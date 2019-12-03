module OrderDetail

open FBlazorShop.App.Model
open Elmish
open FBlazorShop.Web.BlazorClient.Services
open Bolero

type Model = { Order : OrderWithStatus option ; Key : int}


type Message =
    | OrderLoaded of id :int * OrderWithStatus option
    | Reload

let reloadCmd = Cmd.ofMsg Reload

let loadPeriodically remote token id =
    let doWork i = 
        async{ 
            do! Async.Sleep 4000; 
            return! remote.getOrderWithStatus (token,i) 
    }
    Cmd.ofAsync doWork id (fun m -> OrderLoaded(id,m)) (fun _ -> OrderLoaded(0,None))

let init id ={ Order = None; Key = 0}, Cmd.ofMsg (OrderLoaded id)

let update remote message (model : Model, commonModel: Common.State) = 
    match message, commonModel.Authentication with
    | Reload, Common.AuthState.Success auth -> model, loadPeriodically remote auth.Token (model.Key), Cmd.none
    | OrderLoaded(key,_) , Common.AuthState.NotTried -> { model with Key = key }, Cmd.none, Cmd.none
    | OrderLoaded (0 , None), _ -> model,Cmd.none, Cmd.none
    | OrderLoaded (id, order), Common.AuthState.Success auth -> { Order =  order; Key = id }, loadPeriodically remote auth.Token id, Cmd.none
    | _ -> failwith ""

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