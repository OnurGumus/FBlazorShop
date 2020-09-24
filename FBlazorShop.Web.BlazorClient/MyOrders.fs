module MyOrders

open FBlazorShop.App.Model
open Elmish
open FBlazorShop.Web.BlazorClient.Services
open Bolero

type Model = { MyOrders : OrderWithStatus list option}

type Message =
    | OrdersLoaded of OrderWithStatus list
    | Initialized
    | Reload

let reloadCmd = Cmd.ofMsg Reload

let init (remote : PizzaService)  =
    { MyOrders = None } , Cmd.ofMsg Initialized

let update (remote :PizzaService) message (model , commonState: Common.State) =
    match message, commonState.Authentication with
    | Reload , Common.AuthState.Success auth
    | Initialized , Common.AuthState.Success auth-> model, Cmd.OfAsync.either remote.getOrderWithStatuses auth.Token OrdersLoaded raise, Cmd.none
    | OrdersLoaded orders , _ -> { MyOrders = Some orders }, Cmd.none, Cmd.none
    | _ , Common.AuthState.NotTried -> model, Cmd.none, Cmd.none
    | _ -> failwith ""


open Bolero.Html
type OrderList = Template<"wwwroot/MyOrders.html">

let view (model : Model) dispatch =
    concat [
        div [attr.``class`` "main"][
            cond model.MyOrders <| function
                | None -> text "Loading..."
                | Some [] ->
                    concat[
                        h2 [] [ text "No orders placed"]
                        a [attr.href ""; attr.``class`` "btn btn-success"][ text "Order some pizza"]
                    ]
                | Some orders ->
                    let viewOrder (s: OrderWithStatus) =
                        OrderList.OrderItem()
                            .OrderCreatedTime(s.Order.CreatedTime.ToLongDateString())
                            .OrderFormattedTotalPrice(s.Order.FormattedTotalPrice)
                            .OrderOrderId(s.Order.OrderId.ToString())
                            .OrderPizzasCount(s.Order.Pizzas.Length.ToString())
                            .StatusText(s.StatusText)
                            .Version(s.Order.Version.ToString())
                            .Elt()
                    OrderList().Items(forEach orders viewOrder).Elt()
        ]
    ]
