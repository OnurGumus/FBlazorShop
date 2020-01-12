namespace FBlazorShop

open System
open System.Threading.Tasks
open FBlazorShop.App
open FBlazorShop.App.Model
open System.Collections.Generic
open System.Linq
open Akkling
open Domain.Order
open Domain
open Common
open Serilog

type OrderService() =
    interface IOrderService with
        member __.PlaceOrder(order: Order): Task<Result<(string*int),string>> =
            async {
                let orderId = sprintf "order_%s" <| order.OrderId.ToString()
                let corID =  orderId + "~" + Guid.NewGuid().ToString()
                let orderActor = factory orderId
                let commonCommand : Command<_> =
                    {
                        Command = (order |> PlaceOrder) ;
                        CreationDate = DateTime.Now ;
                        CorrelationId =  corID }
                Log.Information "before place"
                let! res = orderActor <? (commonCommand |> Command)
                Log.Information "after place"

                do! Async.Sleep(100)

                match res with
                | {Event = OrderPlaced o ; Version = v}-> return Ok(o.OrderId,v)
                | {Event = OrderRejected (_ , reason)}-> return (Error reason)
                | _ -> return failwith "not supported"

            } |> Async.StartImmediateAsTask
open Projection

type OrderReadOnlyRepo () =
    interface IReadOnlyRepo<OrderEntity> with
        member _.Queryable: Linq.IQueryable<OrderEntity> =
            Projection.orders.AsQueryable()
        member _.ToListAsync(query: Linq.IQueryable<OrderEntity>): Task<IReadOnlyList<OrderEntity>> =
            query.ToList() :> IReadOnlyList<OrderEntity> |> Task.FromResult

//open Projection
//type OrderReadOnlyRepo2 () =
//    interface IReadOnlyRepo<OrdersEntity> with
//        member _.Queryable: Linq.IQueryable<OrdersEntity> =
//            Projection.orders2().AsQueryable()
//        member _.ToListAsync(query: Linq.IQueryable<OrdersEntity>): Task<IReadOnlyList<OrdersEntity>> =
//            query.ToList() :> IReadOnlyList<OrdersEntity> |> Task.FromResult
