module FBlazorShop.Web.BlazorClient.Services

open Bolero.Remoting
open FBlazorShop.App.Model

type public PizzaService = 
    {
        getSpecials : unit -> Async<PizzaSpecial list>
        getToppings : unit -> Async<Topping list>
    }
    interface IRemoteService with
        member __.BasePath = "/pizzas"