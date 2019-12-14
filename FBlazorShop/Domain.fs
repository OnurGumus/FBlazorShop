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
open Akkling.Cluster.Sharding

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
    with static member toEvent ci event  = {
            Event = event
            CreationDate = DateTime.Now;
            CorrelationId =  ci
    }


    module SagaStarter =
        let toOriginatorName (name : string) =  name.Replace("Saga_","_")
        let toSagaName (name : string) = name.Replace("_","Saga_")
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
    let toSendMessage (event, originator) =
        Send("/user/SagaStarter", (event, originator) |> toCheckSagas)

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
                    let event = o |> OrderPlaced |> Event.toEvent ci |> Event
                    let res =
                        mediator <? ((event, untyped mailbox.Self ) |> toSendMessage |> box)
                        |> Async.RunSynchronously
                    match res with
                        | SagaStarter.SagaCheckDone ->
                            return! event |> Persist

                | Command {Command = PlaceOrder o; CorrelationId = ci}, Some _ ->
                    mailbox.Sender() <! (OrderRejected(o,"duplicate") |> Event.toEvent ci |> Event)

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
        let originatorName =  mailbox.Self.Path.Name |> SagaStarter.toOriginatorName
        let rec set state =
            actor {
                    let! msg = mailbox.Receive()
                    match box(msg), state with
                    | :? SubscribeAck as s, _ when  s.Subscribe.Topic = originatorName ->
                        mediator <! box (Send("/user/SagaStarter", SagaStarter.Continue |> SagaStarter.Command))
                        return! set state
                    | PersistentLifecycleEvent ReplaySucceed ,_->
                        mediator <! box (Subscribe(originatorName, untyped mailbox.Self))
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

            let startSaga (originator : Actor.IActorRef) (factory : string -> IEntityRef<_>) =
                let sender = untyped <| mailbox.Sender()
                let saga = originator.Path.Name |> SagaStarter.toSagaName |> factory
                let state = state.Add(originator.Path.Name,(sender,[saga.EntityId]))
                saga <! box(ShardRegion.StartEntity (saga.EntityId))
                state

            actor {
                match! mailbox.Receive() with
                | Command (Continue) ->
                    let sender = untyped <| mailbox.Sender()
                    let originName = sender.Path.Name |> SagaStarter.toOriginatorName
                    let originator,subscribers = state.[originName]
                    let newList = subscribers |> List.filter (fun a -> a <> sender.Path.Name)
                    match newList with
                        | [] -> originator.Tell(SagaCheckDone, untyped mailbox.Self)
                        | _ ->
                            return! set
                                <| state
                                    .Remove(originName)
                                    .Add(originName, (originator,newList))


                | Command(CheckSagas (o, originator)) ->
                    match o with
                    | :? Order.Message ->
                        return! set <| startSaga originator OrderSaga.factory

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
    System.Threading.Thread.Sleep(1000)

