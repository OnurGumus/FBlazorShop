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

type OrderService() =
    interface IOrderService with
        member __.PlaceOrder(order: Order): Task<Result<string,string>> =
            async {
                let orderActor = factory <| sprintf "order-%s"  (order.OrderId.ToString())
                let commonCommand : Common.Command<_> =
                    {
                        Command = (order |> PlaceOrder) ;
                        CreationDate = DateTime.Now ;
                        CorrelationId = Guid.NewGuid().ToString()|> Some }

                let! res = orderActor <? Order.Command(commonCommand)

                match res with
                | {Event = OrderPlaced o }-> return (Ok o.OrderId)
                | {Event = OrderRejected (_ , reason)}-> return (Error reason)

            } |> Async.StartImmediateAsTask

type OrderReadOnlyRepo () =
    interface IReadOnlyRepo<Order> with
        member _.Queryable: Linq.IQueryable<Order> =
            Projection.orders().AsQueryable()
        member _.ToListAsync(query: Linq.IQueryable<Order>): Task<IReadOnlyList<Order>> =
            query.ToList() :> IReadOnlyList<Order> |> Task.FromResult
