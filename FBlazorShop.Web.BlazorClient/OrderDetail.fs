module OrderDetail

open FBlazorShop.App.Model
open Elmish
open FBlazorShop.Web.BlazorClient.Services
open Bolero
open System

type Model = { Order : OrderWithStatus option ; Key : string}


type Message =
    | OrderLoaded of id :string  * OrderWithStatus option * bool
    | Reload

let reloadCmd = Cmd.ofMsg Reload

let loadPeriodically remote token id firstTime =
    let doWork i =
        async{
            if not firstTime then
                do! Async.Sleep 4000;
            return! remote.getOrderWithStatus (token,i)
    }
    Cmd.ofAsync doWork id (fun m -> OrderLoaded(id ,m, false)) (fun _ -> OrderLoaded("",None, true))

let init id ={ Order = None; Key = ""}, Cmd.ofMsg (OrderLoaded (id,None, true))

let update remote message (model : Model, commonModel: Common.State) =
    match message, commonModel.Authentication with
    | Reload, Common.AuthState.Success auth ->
        model, loadPeriodically remote auth.Token (model.Key) true, Cmd.none
    | OrderLoaded(key,_,_) , Common.AuthState.NotTried -> { model with Key = key }, Cmd.none, Cmd.none
    | OrderLoaded ("", None,_), _  -> model,Cmd.none, Cmd.none
    | OrderLoaded (id, order, firstTime), Common.AuthState.Success auth ->
        { Order =  order; Key = id }, loadPeriodically remote auth.Token id firstTime, Cmd.none
    | _ -> failwith ""

open Bolero.Html
open FBlazorShop.ComponentsLibrary
let map  markers =
        comp<Map> ["Zoom" => 13.0; "Markers" => markers ] []

type OrderDetail = Template<"wwwroot\OrderDetail.html">


type OrderDetailView() =
    inherit ElmishComponent<Model, Message>()
    override _.View model dispatch =
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
let view (model : Model) dispatch = ecomp<OrderDetailView,_,_> model dispatch

