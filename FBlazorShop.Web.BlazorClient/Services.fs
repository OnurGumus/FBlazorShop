module FBlazorShop.Web.BlazorClient.Services

open Bolero.Remoting
open FBlazorShop.App.Model

type public PizzaService = 
    {
        getSpecials : unit -> Async<PizzaSpecial list>
    }
    interface IRemoteService with
        member __.BasePath = "/pizzas"