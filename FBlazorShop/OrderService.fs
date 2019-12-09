namespace FBlazorShop

open System
open System.Threading.Tasks
open FBlazorShop.App
open FBlazorShop.App.Model
open System.Collections.Generic
open System.Linq
open Akkling
open Actor
type OrderService() =
    interface IOrderService with
        member __.PlaceOrder(order: Order): Task<string> =
            async {
                let orderActor = Actor.orderFactory <| sprintf "order-%s"  (order.OrderId.ToString())
                let! res = orderActor <? ( order |> PlaceOrder  |> Command)

                match res with
                | OrderPlaced o -> return o.OrderId

            } |> Async.StartImmediateAsTask

type OrderReadOnlyRepo () =
    interface IReadOnlyRepo<Order> with
        member _.Queryable: Linq.IQueryable<Order> =
            Actor.orders().AsQueryable()
        member _.ToListAsync(query: Linq.IQueryable<Order>): Task<IReadOnlyList<Order>> =
            query.ToList() :> IReadOnlyList<Order> |> Task.FromResult
