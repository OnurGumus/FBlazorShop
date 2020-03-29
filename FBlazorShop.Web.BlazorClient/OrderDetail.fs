module OrderDetail

open FBlazorShop.App.Model
open Elmish
open FBlazorShop.Web.BlazorClient.Services
open Bolero
open System

type Model = { Order : OrderWithStatus option ; Key : string ; Version : int}


type Message =
    | OrderLoaded of id :string  * version : int * OrderWithStatus option * bool
    | Reload

let reloadCmd = Cmd.ofMsg Reload

let loadPeriodically remote token  id version firstTime =
    let doWork i =
        let rec getOrder delay =
            async{
                let! order =  remote.getOrderWithStatus (token,i, version)
                match order with
                | Some _ -> return order
                | _  when delay < 1000 ->
                    do! Async.Sleep delay
                    return! getOrder (delay * 2)
                | _ -> return None
            }

        async {
            if not firstTime then
                do! Async.Sleep 4000
            return! getOrder 100

        }
    Cmd.ofAsync doWork id (fun m -> OrderLoaded(id ,version,m, false)) (fun _ -> OrderLoaded("",0,None, true))

let init (id, version) ={ Order = None; Key = ""; Version = 0}, Cmd.ofMsg (OrderLoaded (id, version,None, true))

let update remote message (model : Model, commonModel: Common.State) =
    match message, commonModel.Authentication with
    | Reload, Common.AuthState.Success auth ->
        model, loadPeriodically remote auth.Token (model.Key) (model.Version) true, Cmd.none
    | OrderLoaded(key,_,_,_) , Common.AuthState.NotTried -> { model with Key = key }, Cmd.none, Cmd.none
    | OrderLoaded ("", _,None,_), _  -> model,Cmd.none, Cmd.none
    | OrderLoaded (id, version,order, firstTime), Common.AuthState.Success auth ->
        { Order =  order; Key = id ; Version = version}, loadPeriodically remote auth.Token id version firstTime, Cmd.none
    | _ -> failwith ""

open Bolero.Html
open FBlazorShop.ComponentsLibrary
let map  markers =
        comp<Map> ["Zoom" => 13.0; "Markers" => markers ] []

type OrderDetail = Template<"wwwroot/OrderDetail.html">


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
let view (model : Model) dispatch = ecomp<OrderDetailView,_,_> [] model dispatch

