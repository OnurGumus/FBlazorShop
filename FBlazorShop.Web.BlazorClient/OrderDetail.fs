module OrderDetail

open FBlazorShop.App.Model
open Elmish
open FBlazorShop.Web.BlazorClient.Services
open Bolero

type Model = { Order : OrderWithStatus option}


type Message =
    | OrderLoaded of id :int * OrderWithStatus option
   // | Initialized of id : int

let loadPeriodically remote token id =
    let doWork i = 
        async{ 
            do! Async.Sleep 4000; 
            return! remote.getOrderWithStatus (token,i) 
    }
    Cmd.ofAsync doWork id (fun m -> OrderLoaded(id,m)) (fun _ -> OrderLoaded(0,None))

let init  id  = { Order = None}, Cmd.ofMsg (OrderLoaded id)

let update remote message (model : Model, commonModel: Common.State) = 
    match message, commonModel.Authentication with
    | _ , None-> model, Cmd.none, Cmd.none
    | OrderLoaded (0 , None), _ -> model,Cmd.none, Cmd.none
    | OrderLoaded (id, order), Some auth -> { Order =  order }, loadPeriodically remote auth.Token id, Cmd.none

open Bolero.Html
open FBlazorShop.ComponentsLibrary
let map  markers =
        comp<Map> ["Zoom" => 13.0; "Markers" => markers ] []

type OrderDetail = Template<"wwwroot\OrderDetail.html">
let view (model : Model) dispatch = 
    div [ attr.``class`` "main"][
    cond model.Order <| function
        | Some x -> 
            OrderDetail()
                .OrderCreatedTimeToLongDateString(x.Order.CreatedTime.ToLongDateString())
                .StatusText(x.StatusText)
                .OrderReview(OrderReview.view x.Order dispatch)
                .Map(map (x.MapMarkers)) .Elt()
           
        | _ -> text "Loading..."
    ]