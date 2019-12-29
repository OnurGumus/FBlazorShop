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

[<Literal>]
let resolutionPath = __SOURCE_DIRECTORY__ + @"\..\FBlazorShop.Web"

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

if System.Environment.Is64BitProcess then
    let path = System.Environment.CurrentDirectory
    NativeLibrary.Load(Path.Combine(path, @"net46\SQLite.Interop.dll")) |>ignore

let ctx = Sql.GetDataContext("Data Source=pizza.db;" )


QueryEvents.SqlQueryEvent |> Event.add (fun sql -> Log.Debug ("Executing SQL: {SQL}",sql))



let ser = JsonConvert.SerializeObject
let deser<'t>  = JsonConvert.DeserializeObject<'t>

let handleEvent (envelop : EventEnvelope) =
    Log.Information ("Handle event {@Envelope}", envelop)
    try
        match envelop.Event with
        | :? Message<Order.Command,Order.Event> as order ->

            match order with
            | Event({Event = Order.OrderPlaced o; Version = v}) ->
                let address = o.DeliveryAddress |> ser
                let location = o.DeliveryLocation |> ser
                let pizzas = o.Pizzas |> ser
                let createTime = o.CreatedTime.ToString("o")
                let userId = o.UserId

                let row = ctx.Main.Orders.Create(address,createTime, location, pizzas, userId,int64 v)
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
open FBlazorShop.App.Model
type OrderEntity = Sql.dataContext.``main.OrdersEntity``

let toOrder (x:OrderEntity) = {
    OrderId = x.Id
    DeliveryAddress = x.Address |> deser
    CreatedTime = DateTime.Parse(x.CreatedTime)
    Pizzas = x.Pizzas |> deser
    DeliveryLocation = x.DeliveryLocation |> deser
    UserId = x.UserId
}

let orders  =
    query {
        for x in ctx.Main.Orders  do
            select x
    }

open Akkling.Streams

let init () =
    let source = Actor.readJournal.EventsByTag("default",Offset.Sequence(initOffset))
    System.Threading.Thread.Sleep(100)
    source
    |> Source.runForEach Actor.mat handleEvent
    |> Async.StartAsTask
    |> ignore
