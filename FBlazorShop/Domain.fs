module Domain

open FBlazorShop.App.Model

type Command = PlaceOrder of Order

type Event = OrderPlaced of Order

type Message =
    | Command of Command
    | Event of Event

open Akkling
open Akkling.Persistence


let actorProp (mailbox : Eventsourced<_>)=
  let rec set (state : Order option) =
    actor {
      let! (msg) = mailbox.Receive()
      match msg with
      | Event (OrderPlaced o) when mailbox.IsRecovering () ->
            return! o |> Some |> set
      | Command(PlaceOrder o) ->
            return  o |> OrderPlaced |> Event |> Persist
      | Persisted mailbox (Event(OrderPlaced o as e)) ->
            mailbox.Sender() <! e
            return! o |> Some |> set
      | _ -> invalidOp "not supported"
    }
  set None

let orderFactory str =
    (AkklingHelpers.entityFactoryFor Actor.system "Order"
        <| propsPersist actorProp
        <| None).RefFor AkklingHelpers.DEFAULT_SHARD str


