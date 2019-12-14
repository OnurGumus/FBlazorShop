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
        let toOriginatorName (name : string) =
            let first = name.Replace("_Saga_","_")
            let index = first.IndexOf('_')
            let lastIndex = first.LastIndexOf('_')
            if index <> lastIndex then
                first.Substring(index + 1)
            else
                first

        let toSagaName (name : string) = name.Replace("_","_Saga_")
        let isSaga (name : string) = name.Contains("_Saga_")

        [<Literal>]
        let SagaStarterName = "SagaStarter"

        [<Literal>]
        let SagaStarterPath = "/user/SagaStarter"


        type Command  =
            | CheckSagas of obj * originator : Actor.IActorRef
            | Continue
        type Event = SagaCheckDone
        type Message =
              | Command of Command
              | Event of Event

        let toCheckSagas (event, originator) =
            ((event |> box), originator)
                |> CheckSagas |> Command

        let toSendMessage  mediator (originator : IActorRef<_>) event  =
            let message = Send(SagaStarterPath, (event, untyped originator) |> toCheckSagas)
            (mediator <? (message |> box)) |> Async.RunSynchronously |> function
            | SagaCheckDone -> ()


        let publishEvent (mailbox : Eventsourced<_>) (mediator:IActorRef<_>) event=
            let sender = mailbox.Sender()
            let self = mailbox.Self
            if sender.Path.Name |> isSaga then
                let originatorName = sender.Path.Name |> toOriginatorName
                if originatorName <> self.Path.Name then
                    mediator <! box (Publish(self.Path.Name, event ))
            else
                sender <! event
            mediator <! box (Publish(self.Path.Name, event))
        let cont (mediator : IActorRef<_>) =
            mediator <! box (Send(SagaStarterPath, Continue |> Command))

        let subscriber (mediator : IActorRef<_>) (mailbox : Eventsourced<_>) =
            let originatorName = mailbox.Self.Path.Name |> toOriginatorName
            mediator <! box (Subscribe(originatorName, untyped mailbox.Self))


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
                    //call saga starter and wait till it responds
                    let event = o |> OrderPlaced |> Event.toEvent ci
                    SagaStarter.toSendMessage mediator mailbox.Self event
                    return! event |> Event |> Persist

                | Command {Command = PlaceOrder o; CorrelationId = ci}, Some _ ->
                    //An order can be placed once only
                    mailbox.Sender() <! (OrderRejected(o,"duplicate") |> Event.toEvent ci |> Event)

                | Persisted mailbox (Event({Event = OrderPlaced o } as e)), _ ->
                    SagaStarter.publishEvent mailbox mediator e
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
        let originatorName = mailbox.Self.Path.Name |> SagaStarter.toOriginatorName
        let rec set (state : State) =
            actor {
                    let! msg = mailbox.Receive()
                    match box(msg), state with
                    | :? SubscribeAck as s, _ when s.Subscribe.Topic = originatorName ->
                        // notify saga starter about the subscription completed
                        SagaStarter.cont mediator
                        return! set state

                    | PersistentLifecycleEvent ReplaySucceed ,_->
                        SagaStarter.subscriber mediator mailbox
                        //  take recovery action for the current state
                        return! set state

                    | Recovering mailbox (:? Event as e), _ ->
                        //replay the recovery
                        match e with
                        | StateChanged s -> return! set s

                    | Persisted mailbox msg, _->
                        match msg with
                        | :? Event as e ->
                            match e with
                            | StateChanged state ->
                                //take entry actions of new state
                                return! set state

                        | _ -> return! set state
                    | :? Common.Event<Order.Event> as orderEvent, _ ->
                        match orderEvent with
                        | {Event = Order.OrderPlaced o } ->
                          // decide new state
                          return! Persist(StateChanged (state)|>box)

                        | _ -> return! set state

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
                    //check if all sagas are started. if so issue SagaCheckDone to originator else keep wait
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
                    | :? Common.Event<Order.Event> as e ->
                        match e with
                        | {Event = Order.OrderPlaced _  }  ->
                            //start saga
                            return! set <| startSaga originator OrderSaga.factory
                        | _ -> mailbox.Sender() <! SagaCheckDone

                    | _ ->  mailbox.Sender() <! SagaCheckDone
                    return set state
                | _ -> return! set state

            }
        set Map.empty

    let init =
        let sagaStarter =
            spawn Actor.system
                <| SagaStarter.SagaStarterName
                <| props actorProp

        typed Actor.mediator <! (sagaStarter |> untyped |> Put)

let init () =
    SagaStarter.init
    Order.init |> ignore
    OrderSaga.init |> ignore
    System.Threading.Thread.Sleep(1000)

