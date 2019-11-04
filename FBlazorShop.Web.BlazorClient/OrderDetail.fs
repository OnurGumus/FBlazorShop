module OrderDetail

open FBlazorShop.App.Model
open Elmish
open FBlazorShop.Web.BlazorClient.Services
open Bolero

type Model = { Order : OrderWithStatus option}

type Message =
    | OrderLoaded of OrderWithStatus option

let init (remote : PizzaService) id  =
    { Order = None } , Cmd.ofAsync remote.getOrderWithStatus id OrderLoaded raise

let update message (model : Model) = 
    match message with
    | OrderLoaded order -> { Order =  order }, Cmd.none

open Bolero.Html
let view (model : Model) dispatch = 
    cond model.Order <| function
        | Some x -> text (x.Order.OrderId.ToString())
        | _ -> text "Loading..."