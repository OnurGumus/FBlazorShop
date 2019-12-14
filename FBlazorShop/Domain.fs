module Domain

open FBlazorShop.App.Model
open Akkling
open Akkling.Persistence
open Akka.Cluster.Tools.PublishSubscribe
open System
open AkklingHelpers
open Actor
open Akka
open Akka.Cluster.Sharding

[<AutoOpen>]
module Common =
    type Command<'Command> = {
        Command : 'Command;
        CreationDate  : System.DateTime;
        CorrelationId : string option
    }

    type Event<'Event> = {
        Event : 'Event;
        CreationDate  : System.DateTime;
        CorrelationId : string option
    }

    module SagaStarter =
        [<Literal>]
        let SagaStarterName = "SagaStarter"

        type Command  =
            | CheckSagas of obj * originator : Actor.IActorRef
            | Continue
        type Event = SagaCheckDone
        type Message =
              | Command of Command
              | Event of Event
    let toCheckSagas (event, originator) =
        ((event |> box), originator)
            |> SagaStarter.CheckSagas |> SagaStarter.Command

module Order =

    type Command = PlaceOrder of Order

    type Event =
        | OrderPlaced of Order
        | OrderRejected of Order * reason : string

    type Message =
        | Command of Common.Command<Command>
        | Event of Common.Event<Event>
        with interface IDefaultTag

    let actorProp (mediator : IActorRef<_>) (mailbox : Eventsourced<_>)=
        let rec set state =
            actor {
                let! msg = mailbox.Receive()
                match msg, state with
                | Recovering mailbox (Event {Event = OrderPlaced o}), _ ->
                    return! o |> Some |> set
                | Command{Command = PlaceOrder o; CorrelationId = ci}, None ->
                    let event = {
                        Event = o |> OrderPlaced;
                        CreationDate = DateTime.Now;
                        CorrelationId = ci } |> Event
                    let res = (mediator <? box (Send("/user/SagaStarter", (event, untyped mailbox.Self) |> toCheckSagas))) |> Async.RunSynchronously
                    match res with
                        //|> function
                        | SagaStarter.SagaCheckDone ->
                                return! event |> Persist
                | Command {Command = PlaceOrder o; CorrelationId = ci}, Some _ ->
                    mailbox.Sender() <! {
                        Event = OrderRejected(o,"duplicate")
                        CreationDate = DateTime.Now;
                        CorrelationId = ci
                    }

                | Persisted mailbox (Event({Event = OrderPlaced o } as e)), _ ->
                    mailbox.Sender() <! e
                    mediator <! box (Publish(mailbox.Self.Path.Name, e))
                    return! o |> Some |> set
                | _ -> invalidOp "not supported"
            }
        set None
    let init =
        AkklingHelpers.entityFactoryFor Actor.system "Order"
            <| propsPersist (actorProp (typed Actor.mediator))
            <| false

    let factory entityId =
           init.RefFor AkklingHelpers.DEFAULT_SHARD entityId

module OrderSaga =
    type State =
        | Started
        | OutForDelivery
        | Delivered

    type Event =
        | StateChanged of State
        with interface IDefaultTag

    let actorProp (mediator : IActorRef<_>)(mailbox : Eventsourced<obj>)=
        let rec set state =
            actor {
                    let! msg = mailbox.Receive()
                    match box(msg), state with
                    | :? SubscribeAck as s, _ when  s.Subscribe.Topic = mailbox.Self.Path.Name.Replace("Saga","") ->
                        mediator <! box (Send("/user/SagaStarter", SagaStarter.Continue |> SagaStarter.Command))
                        return! set state
                    | PersistentLifecycleEvent ReplaySucceed ,_->
                        mediator <! box (Subscribe(mailbox.Self.Path.Name.Replace("Saga",""), untyped mailbox.Self))
                        return! set state
                    | Recovering mailbox (:? Event as e), _ ->
                        match e with
                        | StateChanged s -> return! set (box(s))
                    | Persisted mailbox e, _-> return! set e
                    | :? Event as e, _  ->
                        match e with
                        | StateChanged state ->
                           return! Persist(StateChanged (state)|>box)
                    | _ -> return! set state
            }
        set Started
    let init =
        (AkklingHelpers.entityFactoryFor Actor.system "OrderSaga"
        <| propsPersist (actorProp(typed Actor.mediator))
        <| true)

    let factory entityId =
        init.RefFor AkklingHelpers.DEFAULT_SHARD entityId

module SagaStarter =
    open SagaStarter
    let actorProp (mailbox : Actor<_>)=
        let rec set (state  : Map<string, (Actor.IActorRef * string list)>) =
            actor {
                match! mailbox.Receive() with
                | Command (Continue) ->
                    let sender = untyped <| mailbox.Sender()
                    let originName = sender.Path.Name.Replace("Saga","")
                    let originator,subscribers = state.[originName]
                    let newList = subscribers |> List.filter (fun a -> a <> sender.Path.Name)
                    match newList with
                        | [] -> originator.Tell(SagaCheckDone, untyped mailbox.Self)
                        | _ ->
                            return! set
                                <| state
                                    .Remove(originName)
                                    .Add(originName, (originator,newList))


                   // let sender = mailbox.Sender()
                | Command(CheckSagas (o, originator)) ->
                    match o with
                    | :? Order.Message ->
                       let sender = untyped <| mailbox.Sender()
                       let off =  OrderSaga.factory <| originator.Path.Name.Replace("_","Saga_")
                       let state = state.Add(originator.Path.Name,(sender,[off.EntityId]))

                       off <! box(ShardRegion.StartEntity (off.EntityId))
                       return! set state
                    | _ ->  mailbox.Sender() <! SagaCheckDone
                    return set state
                | _ -> return! set state

            }
        set Map.empty

let init () =
    let sagaStarter =
        spawn Actor.system
            <| SagaStarter.SagaStarterName
            <| props SagaStarter.actorProp

    typed Actor.mediator <! (sagaStarter |> untyped |> Put)
    Order.init |> ignore
    OrderSaga.init |> ignore
  //  z.Underlying.Tell( ShardRegion.StartEntity("t"), null)
    //let orderSaga = OrderSaga.factory "test"
    //orderSaga <! box (OrderSaga.StateChanged(OrderSaga.OutForDelivery))
    System.Threading.Thread.Sleep(1000)

