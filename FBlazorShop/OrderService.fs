namespace FBlazorShop

open System
open System.Threading.Tasks
open FBlazorShop.App
open FBlazorShop.App.Model
open System.Collections.Generic
open System.Linq
open Domain.Order
open Common
open Serilog
open CommandHandler

type OrderService() =
    interface IOrderService with
        member __.PlaceOrder(order: Order): Task<Result<(string*int),string>> =
            async {
                let orderId = sprintf "order_%s" <| order.OrderId.ToString()
                let corID =  orderId |> SagaStarter.toCid
                let orderActor = factory orderId
                let commonCommand : Command<_> =
                    {
                        Command = PlaceOrder order
                        CreationDate = Domain.clockInstance.GetCurrentInstant()
                        CorrelationId =  corID }
                let c =
                    {   Cmd = commonCommand ;
                        EntityRef = orderActor;
                        Filter = (function OrderPlaced _ | OrderRejected _ -> true |  _ -> false) } |> Execute

                Log.Information "before place"
                let! res = Actor.subscribeForCommand c
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

