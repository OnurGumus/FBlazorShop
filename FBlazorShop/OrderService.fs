namespace FBlazorShop

open System
open System.Threading.Tasks
open FBlazorShop.App
open FBlazorShop.App.Model
open System.Collections.Generic
open System.Linq

type OrderService() =
    static let orders = List<Order>()

    member __.Orders = orders

    interface IOrderService with
        member __.PlaceOrder(order: Order): Task<int> = 
            let order = { order with OrderId = orders.Count + 1 }
            orders.Add(order)
            Task.FromResult (order.OrderId)

type OrderReadOnlyRepo (orderService : OrderService) =
    interface IReadOnlyRepo<Order> with
        member this.Queryable: Linq.IQueryable<Order> = 
            orderService.Orders.AsQueryable()
        member this.ToListAsync(query: Linq.IQueryable<Order>): Task<IReadOnlyList<Order>> = 
            query.ToList() :> IReadOnlyList<Order> |> Task.FromResult
