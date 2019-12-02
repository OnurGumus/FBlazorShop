module FBlazorShop.Web.BlazorClient.Services

open Bolero.Remoting
open FBlazorShop.App.Model
open Common

type public PizzaService = 
    {
        getSpecials : unit -> Async<PizzaSpecial list>
        getToppings : unit -> Async<Topping list>
        getOrders : unit -> Async<Order list>
        getOrderWithStatuses : unit -> Async<OrderWithStatus list>
        getOrderWithStatus : int -> Async<OrderWithStatus option>
        placeOrder : Order -> Async<int>
        signIn : string * string -> Async<Result<Authentication,string>>
    }
    interface IRemoteService with
        member __.BasePath = "/pizzas"