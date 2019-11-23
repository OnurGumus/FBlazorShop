module OrderDetail

open FBlazorShop.App.Model
open Elmish
open FBlazorShop.Web.BlazorClient.Services
open Bolero

type Model = { Order : OrderWithStatus option}


type Message =
    | OrderLoaded of id :int * OrderWithStatus option

let loadPeriodically remote id =
    let doWork i = 
        async{ 
            do! Async.Sleep 4000; 
            return! remote.getOrderWithStatus i 
    }
    Cmd.ofAsync doWork id (fun m -> OrderLoaded(id,m)) (fun m -> OrderLoaded(id,None))

let init (remote : PizzaService) id  =
    { Order = None } , Cmd.ofAsync remote.getOrderWithStatus id (fun m -> OrderLoaded(id,m)) raise

let update remote message (model : Model) = 
    match message with
    | OrderLoaded (id , None) -> model,Cmd.none
    | OrderLoaded (id, order) -> { Order =  order }, loadPeriodically remote id

open Bolero.Html

type OrderDetail = Template<"wwwroot\OrderDetail.html">
let view (model : Model) dispatch = 
    div [ attr.``class`` "main"][
    cond model.Order <| function
        | Some x -> 
            OrderDetail()
                .OrderCreatedTimeToLongDateString(x.Order.CreatedTime.ToLongDateString())
                .StatusText(x.StatusText)
                .OrderReview(OrderReview.view x.Order dispatch)
                .Elt()
           
        | _ -> text "Loading..."
    ]