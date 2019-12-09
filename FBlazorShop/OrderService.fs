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
    static let orders = List<Order>()

    member __.Orders = orders

    interface IOrderService with
        member __.PlaceOrder(order: Order): Task<string> =
            async {
                orders.Add(order)
                let orderActor = Actor.orderFactory <| sprintf "order-%s"  (order.OrderId.ToString())
                let! res = orderActor <? Actor.Command (Actor.PlaceOrder order)

                match (res ) with
                | Actor.OrderPlaced o -> return o.OrderId
                | _ -> return failwith ""
            } |> Async.StartImmediateAsTask

type OrderReadOnlyRepo (orderService : OrderService) =
    interface IReadOnlyRepo<Order> with
        member this.Queryable: Linq.IQueryable<Order> =
            Actor.orders().AsQueryable()
        member this.ToListAsync(query: Linq.IQueryable<Order>): Task<IReadOnlyList<Order>> =
            query.ToList() :> IReadOnlyList<Order> |> Task.FromResult
