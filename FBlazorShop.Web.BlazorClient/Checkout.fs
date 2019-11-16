module Checkout


open FBlazorShop.App.Model
open Elmish
open FBlazorShop.Web.BlazorClient.Services
open Bolero

type Model = { Order : Order option}


type Message =
    //| OrderLoaded of id :int * OrderWithStatus option
    | OrderPlaced of Order : Order
    | OrderAccepted of orderId : int

let init (remote : PizzaService) (order : Order option)  =
    { Order =  order } , Cmd.none// Cmd.ofAsync remote.getOrderWithStatus id (fun m -> OrderLoaded(id,m)) raise

let update remote message (model : Model) = 
    match message with
   // | OrderLoaded (_, order) -> { Order =  order }, Cmd.none
   
    | OrderPlaced o -> 
        let cmd = Cmd.ofAsync remote.placeOrder o OrderAccepted raise
        model, cmd
    | OrderAccepted _ -> invalidOp "should not happen"

open Bolero.Html

type OrderDetail = Template<"wwwroot\Checkout.html">
let view (model : Model) dispatch = 
    div [ attr.``class`` "main"][
    cond model.Order <| function
        | Some x -> 
            OrderDetail()
                .OrderReview(OrderReview.view x dispatch)
                .PlaceOrder(fun _ -> dispatch (OrderPlaced x))
                .Elt()
           
        | _ -> text "Loading..."
    ]