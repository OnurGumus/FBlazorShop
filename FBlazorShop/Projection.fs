module Projection

open Akka.Persistence.Query
open Newtonsoft.Json
open Domain
open Common
open Serilog

let ser = JsonConvert.SerializeObject
let deser<'t>  = JsonConvert.DeserializeObject<'t>

let handleEvent (envelop : EventEnvelope) =
    Log.Information ("Handle event {@Envelope}", envelop)
    try
        match envelop.Event with
        | :? Message<Order.Command,Order.Event> as order ->

            match order with
            | Event({Event = Order.OrderPlaced o}) ->
                let address = o.DeliveryAddress |> ser
                let location = o.DeliveryLocation |> ser
                let pizzas = o.Pizzas |> ser
                let createTime = o.CreatedTime.ToString("o")
                let userId = o.UserId

                let row = Actor.ctx.Main.Orders.Create(address,createTime, location, pizzas, userId)
                row.Id <- o.OrderId.ToString()

            | _ -> ()

        | _ -> ()
        Actor.ctx.Main.Offsets.Individuals.Orders.OffsetCount
            <- (envelop.Offset :?>Sequence ).Value
        Actor.ctx.SubmitUpdates()

     with e -> printf "%A" e


let initOffset  =
    Actor.ctx.Main.Offsets.Individuals.Orders.OffsetCount

open System
open FBlazorShop.App.Model

let orders () =
    Log.Information "get orders"
    Actor.ctx.Main.Orders
    |> Seq.map(fun x ->
        {   OrderId = x.Id
            DeliveryAddress = x.Address |> deser
            CreatedTime = DateTime.Parse(x.CreatedTime)
            Pizzas = x.Pizzas |> deser
            DeliveryLocation = x.DeliveryLocation |> deser
            UserId = x.UserId
        } : Order)

open Akkling.Streams

let init () =
    let source = Actor.readJournal.EventsByTag("default",Offset.Sequence(initOffset))
    System.Threading.Thread.Sleep(100)
    source
    |> Source.runForEach Actor.mat handleEvent
    |> Async.StartAsTask
    |> ignore
