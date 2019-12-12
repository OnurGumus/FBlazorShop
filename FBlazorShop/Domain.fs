module Domain

open FBlazorShop.App.Model
open Akkling
open Akkling.Persistence
open Akka.Cluster.Tools.PublishSubscribe
open System

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

        type Command  = CheckSagas of obj
        type Event = SagaCheckDone
        type Message =
              | Command of Command
              | Event of Event
    let toCheckSagas event = event |> box |> SagaStarter.CheckSagas |> SagaStarter.Command

module Order =

    type Command = PlaceOrder of Order

    type Event =
        | OrderPlaced of Order
        | OrderRejected of Order * reason : string

    type Message =
        | Command of Common.Command<Command>
        | Event of Common.Event<Event>

    let actorProp (mediator : IActorRef<_>) (mailbox : Eventsourced<_>)=
        let rec set state =
            actor {
                let! msg = mailbox.Receive()
                match msg, state with
                | Event {Event = OrderPlaced o},_ when mailbox.IsRecovering () ->
                    return! o |> Some |> set
                | Command{Command = PlaceOrder o; CorrelationId = ci}, None ->
                    let event = {
                        Event = o |> OrderPlaced;
                        CreationDate = DateTime.Now;
                        CorrelationId = ci } |> Event
                    return!
                        mediator <? box (Send("/user/SagaStarter", event |> toCheckSagas))
                        |> Async.RunSynchronously
                        |> function
                        | SagaStarter.SagaCheckDone ->
                             event |> Persist
                | Command {Command = PlaceOrder o; CorrelationId = ci}, Some _ ->
                    mailbox.Sender()
                        <!
                            {  Event = (OrderRejected(o,"duplicate"));
                                CreationDate = DateTime.Now;
                                CorrelationId = ci}


                | Persisted mailbox (Event({Event = OrderPlaced o }as e)), _ ->
                    mailbox.Sender() <! e
                    mediator <! box (Publish(mailbox.Self.Path.Name, e))
                    return! o |> Some |> set
                | _ -> invalidOp "not supported"
            }
        set None

    let factory entityId =
        (AkklingHelpers.entityFactoryFor Actor.system "Order"
            <| propsPersist (actorProp (typed Actor.mediator))
            <| None)
            .RefFor AkklingHelpers.DEFAULT_SHARD entityId

module SagaStarter =
    open SagaStarter
    let actorProp (mailbox : Actor<_>)=
        let rec set () =
            actor {
                match! mailbox.Receive() with
                | Command(CheckSagas (o)) ->
                    match  o with
                    | :? Order.Message -> printf "starting saga"
                        //start the saga
                    | _ -> ()
                    mailbox.Sender() <! SagaCheckDone
                    return set()
                | _ -> return! set()

            }
        set ()

let init () =
    let sagaStarter =
        spawn Actor.system
            <| SagaStarter.SagaStarterName
            <| props SagaStarter.actorProp

    typed Actor.mediator <! (sagaStarter |> untyped |> Put)
    Order.factory "Order_ZERO" |> ignore
    System.Threading.Thread.Sleep(1000)

