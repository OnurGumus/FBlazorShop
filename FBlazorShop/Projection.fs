module Projection

open Akka.Persistence.Query
open Newtonsoft.Json
open Domain
open Common
open Serilog
open FSharp.Data.Sql
open System.Runtime.InteropServices

open Actor
open System.IO
open FSharp.Data.Sql.Common
open Serilog
open Akka.Persistence.Sqlite
open Akka.Persistence.Query.Sql


[<Literal>]
let resolutionPath = __SOURCE_DIRECTORY__ + @"\.."

[<Literal>]
let connectionString =
    @"Data Source=" + __SOURCE_DIRECTORY__ + @"\..\FBlazorShop.Web\pizza.db;"

type Sql =
    SqlDataProvider<
            Common.DatabaseProviderTypes.SQLITE,
            SQLiteLibrary = Common.SQLiteLibrary.SystemDataSQLite,
            ConnectionString = connectionString,
            ResolutionPath = resolutionPath,
            CaseSensitivityChange = Common.CaseSensitivityChange.ORIGINAL>


let ctx = Sql.GetDataContext("Data Source=pizza.db;" )

QueryEvents.SqlQueryEvent |> Event.add (fun sql -> Log.Debug ("Executing SQL: {SQL}",sql))

open FBlazorShop.App.Model

let ser = JsonConvert.SerializeObject
let deser<'t>  = JsonConvert.DeserializeObject<'t>

let markAsDelivered (o: Order) =
    let maybe = query {
        for p in ctx.Main.Orders do
        where (p.Id = o.OrderId)
        select (Some p)
        exactlyOneOrDefault
    }
    match maybe with
    | Some order ->
        order.DeliveryStatus <- DeliveryStatus.Delivered |> ser
        order.Version <- o.Version |> int64
        ctx.SubmitUpdates()
    | None -> ()

let handleEvent (envelop : EventEnvelope) =
    Log.Information ("Handle event {@Envelope}", envelop)
    try
        match envelop.Event with
        | :? Message<Order.Command,Order.Event> as order ->

            match order with
            | Event({Event = Order.MarkedDelivered o}) ->
                markAsDelivered o
            | Event({Event = Order.OrderPlaced o}) ->
                let address = o.DeliveryAddress |> ser
                let location = o.DeliveryLocation |> ser
                let pizzas = o.Pizzas |> ser
                let createTime = o.CreatedTime.ToString("o")
                let userId = o.UserId
                let deliveryStatus = o.DeliveryStatus |> ser
                let currentLocation = o.CurrentLocation |> ser
                let row = ctx.Main.Orders.Create(address,createTime, currentLocation,  location, deliveryStatus,pizzas, userId,int64 o.Version)
                row.Id <- o.OrderId.ToString()

            | _ -> ()

        | _ -> ()
        ctx.Main.Offsets.Individuals.Orders.OffsetCount
            <- (envelop.Offset :?>Sequence ).Value
        ctx.SubmitUpdates()

     with e -> printf "%A" e


let initOffset  =
    ctx.Main.Offsets.Individuals.Orders.OffsetCount

open System

type OrderEntity = Sql.dataContext.``main.OrdersEntity``

let toOrder (x:OrderEntity) = {
    OrderId = x.Id
    DeliveryAddress = x.Address |> deser
    CreatedTime = DateTime.Parse(x.CreatedTime)
    Pizzas = x.Pizzas |> deser
    DeliveryLocation = x.DeliveryLocation |> deser
    UserId = x.UserId
    DeliveryStatus = x.DeliveryStatus |> deser
    CurrentLocation = x.CurrentLocation |> deser
    Version = int x.Version
}

let orders  =
    query {
        for x in ctx.Main.Orders  do
            select x
    }

open Akkling.Streams


let readJournal =
    PersistenceQuery.Get(system)
        .ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);


let init () =
    let source = readJournal.EventsByTag("default",Offset.Sequence(initOffset))
    System.Threading.Thread.Sleep(100)
    source
    |> Source.runForEach Actor.mat handleEvent
    |> Async.StartAsTask
    |> ignore
