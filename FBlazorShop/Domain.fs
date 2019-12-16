module Domain

open FBlazorShop.App.Model
open Akkling
open Akkling.Persistence
open AkklingHelpers
open Actor
open Akka
open Common

module Order =
    type Command = PlaceOrder of Order

    type Event =
        | OrderPlaced of Order
        | OrderRejected of Order * reason : string

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
                | _ -> return Unhandled
            }
        set None
    let init =
        AkklingHelpers.entityFactoryFor Actor.system "Order"
            <| propsPersist (actorProp (typed Actor.mediator))
            <| false

    let factory entityId =
           init.RefFor AkklingHelpers.DEFAULT_SHARD entityId


module Delivery =
    type Command = StartDelivery of Order

    type Event =
        | Delivered of Order
      //  | Deliver
      //  | OrderRejected of Order * reason : string
    type State = NotStarted | Delivering of LatLong * Order | Delivered of Order


    let actorProp (mediator : IActorRef<_>) (mailbox : Eventsourced<_>)=
        let rec set (state : State) =
            actor {
                let! msg = mailbox.Receive()
                match msg, state with
                | Recovering mailbox (Event {Event = Delivered o}), _ ->
                    return! o |> Delivered |> set

                | Command{Command = StartDelivery o; CorrelationId = ci}, NotStarted ->
                    //call saga starter and wait till it responds
                    let event = o |> Delivered |> Event.toEvent ci
                    SagaStarter.toSendMessage mediator mailbox.Self event
                    return! event |> Event |> Persist

                //| Command {Command = PlaceOrder o; CorrelationId = ci}, Some _ ->
                //    //An order can be placed once only
                //    mailbox.Sender() <! (OrderRejected(o,"duplicate") |> Event.toEvent ci |> Event)

                | Persisted mailbox (Event({Event = Delivered o } as e)), _ ->
                   SagaStarter.publishEvent mailbox mediator e
                   return! set (Delivered o)
                | _ -> return Unhandled
            }
        set NotStarted
    let init =
        AkklingHelpers.entityFactoryFor Actor.system "Delivery"
            <| propsPersist (actorProp (typed Actor.mediator))
            <| false

    let factory entityId =
           init.RefFor AkklingHelpers.DEFAULT_SHARD entityId

module OrderSaga =
    type State =
        | Started
        | ProcessingOrder of Order
        | OutForDelivery of Order
        | Delivered of Order

    type Event =
        | StateChanged of State
        with interface IDefaultTag

    let actorProp (mediator : IActorRef<_>)(mailbox : Eventsourced<obj>)=
        let rec set (state : State) =
            actor {
                let! msg = mailbox.Receive()
                match msg, state with
                | SagaStarter.SubscrptionAcknowledged mailbox _, _  ->
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

                | Persisted mailbox (:? Event as e ), _->
                    match e with
                    | StateChanged (ProcessingOrder o) ->
                        let rawGuid = (mailbox.Self.Path.Name |> SagaStarter.toRawoGuid)
                        let delivery = Delivery.factory <| "Delivery_" + rawGuid
                        delivery<!
                            ({ Command =  Delivery.StartDelivery o;
                                CreationDate = System.DateTime.Now;
                                CorrelationId = Some rawGuid} |> Common.Command)
                        //take entry actions of new state
                        return! set state

                    | _ -> return! set state
                | :? Common.Event<Order.Event> as orderEvent, _ ->
                    match orderEvent with
                    | {Event = Order.OrderPlaced o } ->
                      // decide new state
                      let state = ProcessingOrder o
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

let sagaCheck (o : obj)=
    match o with
    | :? Common.Event<Order.Event> as e ->
        match e with
        | {Event = Order.OrderPlaced _  }  -> Some OrderSaga.factory
        | _ -> None
    | _ -> None

let init () =
    SagaStarter.init Actor.system Actor.mediator sagaCheck
    Order.init |> ignore
    OrderSaga.init |> ignore
    System.Threading.Thread.Sleep(1000)

