module Domain


open FBlazorShop.App.Model
open Akkling
open Akkling.Persistence
open AkklingHelpers
open Akka
open Common
open Akka.Cluster.Sharding
open Serilog
open System
open Akka.Cluster.Tools.PublishSubscribe

[<Literal>]
let DEFAULT_SHARD = "default-shard"
let shardResolver = fun _ -> DEFAULT_SHARD

module Order =
    type Command =
        | PlaceOrder of Order
        | GetOrderDetails
        | MarkAsDelivered
        | SetCurrentLocation of LatLong

    type Event =
        | NoOrdersPlaced
        | OrderDetailsFound of Order
        | OrderPlaced of Order
        | OrderRejected of Order * reason : string
        | MarkedDelivered of Order
        | LocationUpdated of Order * LatLong


    let actorProp (mediator : IActorRef<Publish>) (mailbox : Eventsourced<_>)=
       let mediatorS = retype mediator
       let publish  =  SagaStarter.publishEvent mailbox mediator
       let sendToSagaStarter =  SagaStarter.toSendMessage mediatorS mailbox.Self
       let rec set (state : Order option * int)=
           actor {
               let! msg = mailbox.Receive()
               Log.Debug("Message {@MSG}", box msg)
               match msg, state with
               | Recovering mailbox (Event {Event = OrderPlaced o; Version = version}), _ ->
                   return! (o |> Some, version) |> set

               | Command{Command = MarkAsDelivered;CorrelationId =  ci},
                   (Some ( (* { DeliveryStatus = DeliveryStatus.OutForDelivery} as *) o), v) ->

                    let event = Event.toEvent ci (MarkedDelivered {o with DeliveryStatus = Delivered ; Version = v + 1}) (v + 1)
                    sendToSagaStarter event  ci
                    return! event |> Event |> Persist

               | Command{Command = MarkAsDelivered;CorrelationId = ci},
                   (Some ({ DeliveryStatus = DeliveryStatus.Delivered} as o), v) ->

                   Event.toEvent ci (MarkedDelivered o) (v) |> publish
                   return! set state

               | Command{Command = SetCurrentLocation(latLong);CorrelationId = ci}, (Some o, v) ->
                   let event = Event.toEvent ci (LocationUpdated ({o with DeliveryStatus = OutForDelivery; CurrentLocation = latLong; Version = v + 1},latLong)) (v + 1)
                   sendToSagaStarter event ci
                   return! event |> Event |> Persist

               | Command{Command = GetOrderDetails;CorrelationId = ci}, (None, v) ->
                   Event.toEvent ci NoOrdersPlaced v |> publish
                   return! set state

               | Command{Command = GetOrderDetails; CorrelationId = ci}, (Some o, v) ->
                   Event.toEvent ci (OrderDetailsFound o) v |> publish
                   return! set state


               | Command{Command = PlaceOrder o; CorrelationId = ci}, (None, version) ->
                   //call saga starter and wait till it responds
                   let event =  Event.toEvent ci ({o with Version = version + 1} |> OrderPlaced ) (version + 1)
                   sendToSagaStarter event ci
                   return! event |> Event |> Persist

               | Command {Command = PlaceOrder o; CorrelationId = ci}, (Some _, version) ->
                   //An order can be placed once only
                   mailbox.Sender() <! ( (Event.toEvent ci (OrderRejected(o,"duplicate")) version ) |> Event)
                   return! Ignore

               | Persisted mailbox (Event({Event = OrderPlaced o ; Version = v} as e)), _ ->
                   Log.Information "persisted"
                   publish e
                   return! (Some o, v) |> set

               | Persisted mailbox (Event({Event = MarkedDelivered o ; Version = v} as e)), _ ->
                   publish e
                   return! (Some o, v) |> set

               | Persisted mailbox (Event({Event = LocationUpdated (o,_) ; Version = v} as e)), _ ->
                   publish e
                   return! (Some o, v) |> set
               | _ -> return Unhandled
           }
       set (None, 0)


    let init =
        AkklingHelpers.entityFactoryFor Actor.system shardResolver "Order"
            <| propsPersist (actorProp (typed Actor.mediator))
            <| false

    let factory entityId =
           init.RefFor DEFAULT_SHARD entityId


module Delivery =
    type Command =
        | StartDelivery of Order
        | GetDeliveryStatus
    type Event =
        | Delivered of Order
        | NoDeliveries
        | DeliveryInProgress
        | DeliveryCompleted
      //  | Deliver
      //  | OrderRejected of Order * reason : string
    type State =
        | NotStarted
        | Delivering of LatLong * Order
        | DeliveryCompleted of Order


    let actorProp (mediator : IActorRef<_>) (mailbox : Eventsourced<_>)=
        let mediatorS = retype mediator
        let publish  =  SagaStarter.publishEvent mailbox mediator
        let sendToSagaStarter =  SagaStarter.toSendMessage mediatorS mailbox.Self
        let rec set (state : State * int) =
            actor {
                let! msg = mailbox.Receive()
                match msg, state with
                | Recovering mailbox (Event {Event = Delivered o; Version = v}), _ ->
                    return! (o |> DeliveryCompleted,v) |> set

                | Command{Command = StartDelivery o; CorrelationId = ci}, (NotStarted,v) ->
                    //call saga starter and wait till it responds
                    let event = Event.toEvent ci ( o |> Delivered ) (v + 1)
                    sendToSagaStarter event ci
                    return! event |> Event |> Persist
                | Command{Command = GetDeliveryStatus; CorrelationId = ci}, (status,v) ->
                    let event =
                        match status with
                        | State.DeliveryCompleted _ -> Event.DeliveryCompleted
                        | NotStarted -> NoDeliveries
                        | Delivering _ -> DeliveryInProgress

                    //call saga starter and wait till it responds
                    let event = Event.toEvent ci event v
                    sendToSagaStarter event ci
                    return! event |> Event |> Persist


                | Persisted mailbox (Event({Event = Delivered o ; Version  = v} as e)), _ ->
                   publish e
                   return! set ((DeliveryCompleted o), v)
                | _ -> return Unhandled
            }
        set (NotStarted, 0)
    let init =
        Log.Information "order init"

        AkklingHelpers.entityFactoryFor Actor.system shardResolver "Delivery"
            <| propsPersist (actorProp (typed Actor.mediator))
            <| false

    let factory entityId =
           init.RefFor DEFAULT_SHARD entityId

module OrderSaga =
    type State =
        | NotStarted
        | Started
        | ProcessingOrder of Order
       // | OutForDelivery of Order
       // | Delivered of Order

    type Event =
        | StateChanged of State
        with interface IDefaultTag

    let actorProp (mediator : IActorRef<_>)(mailbox : Eventsourced<obj>)=
        let rec set (state : State) =
            let cid = (mailbox.Self.Path.Name |> SagaStarter.toRawGuid)
            let deliveryActor = Delivery.factory <| "Delivery_" + cid
            let startDeliveryCmd o = ({ Command =  Delivery.StartDelivery o;
                                           CreationDate = System.DateTime.Now;
                                           CorrelationId =  cid} |> Common.Command)
            let markDelivered () =
                ({ Command =  Order.MarkAsDelivered;
                    CreationDate = System.DateTime.Now;
                    CorrelationId = cid} |> Common.Command)

            let orderActor =
                mailbox.Self.Path.Name
                |> SagaStarter.toOriginatorName
                |> Order.factory

            actor {
                let! msg = mailbox.Receive()
                match msg, state with

                | SagaStarter.SubscrptionAcknowledged mailbox _, _  ->
                    // notify saga starter about the subscription completed
                    match state with
                    | NotStarted ->   return! Started|> StateChanged |>box |> Persist
                    | Started ->
                        let command = {
                            Command = Order.Command.GetOrderDetails
                            CreationDate = DateTime.Now
                            CorrelationId =  cid} |> Message.Command
                        orderActor <! command
                        return! set state

                    | ProcessingOrder _ ->
                        deliveryActor<!
                            ({ Command =  Delivery.GetDeliveryStatus
                               CreationDate = System.DateTime.Now
                               CorrelationId =  cid} |> Common.Command)

                        return! set state

                | PersistentLifecycleEvent ReplaySucceed ,_->
                    SagaStarter.subscriber mediator mailbox
                    return! set state
                   // SagaStarter.checkStarted mediator
                    //  take recovery action for the final state

                | Recovering mailbox (:? Event as e), _ ->
                    //replay the recovery
                    match e with
                    | StateChanged s -> return! set s

                | Persisted mailbox (:? Event as e ), _->
                    match e with
                    | StateChanged Started ->
                     SagaStarter.cont mediator
                     return! set state

                    //take entry actions of new state
                    | StateChanged (ProcessingOrder o as newState )->
                        deliveryActor<! (startDeliveryCmd o)
                        return! set newState
                    | _ ->  return! set state
                | :? Common.Event<Order.Event> as orderEvent, _ ->
                    // decide new state
                    match orderEvent with
                    | {Event = Order.OrderPlaced o }
                    | {Event = Order.OrderDetailsFound o } ->
                        let m = StateChanged (ProcessingOrder o)|>box
                        return! Persist(m)

                    | {Event = Order.MarkedDelivered _ }
                    | {Event = Order.NoOrdersPlaced } ->
                         mailbox.Parent() <! Passivate(Actor.PoisonPill.Instance)
                         return! set state

                    | _ -> return! set state
                | :? Common.Event<Delivery.Event> as deliveryEvent, state ->
                    // decide new state
                    match deliveryEvent , state with
                    | {Event = Delivery.NoDeliveries } , ProcessingOrder o ->
                        deliveryActor <! startDeliveryCmd o
                    | {Event = Delivery.DeliveryCompleted }, _
                    | {Event = Delivery.Delivered _ } ,_->
                        orderActor <!  markDelivered()
                        return! set state
                    | _ ->  return! Unhandled

                | _ -> return! set state
            }
        set NotStarted

    let init =
        (AkklingHelpers.entityFactoryFor Actor.system shardResolver "OrderSaga"
        <| propsPersist (actorProp(typed Actor.mediator))
        <| true)

    let factory entityId =
        init.RefFor DEFAULT_SHARD entityId

let sagaCheck (o : obj)=
    match o with
    | :? Event<Order.Event> as e ->
        match e with
        | {Event = Order.OrderPlaced _} -> [ (OrderSaga.factory, "")]
        | _ -> []
    | _ -> []

let init () =
    SagaStarter.init Actor.system Actor.mediator sagaCheck
    Order.init |> sprintf "Order initialized: %A" |> Log.Debug
    Delivery.init |> sprintf "Delivery initialized: %A" |> Log.Debug
    OrderSaga.init |> sprintf "OrderSaga initialized %A" |> Log.Debug
    System.Threading.Thread.Sleep(1000)

