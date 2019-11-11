module MyOrders

open FBlazorShop.App.Model
open Elmish
open FBlazorShop.Web.BlazorClient.Services
open Bolero

type Model = { MyOrders : OrderWithStatus list option}

type Message =
    | OrdersLoaded  of OrderWithStatus list

let init (remote : PizzaService)  =
    { MyOrders = None } , Cmd.ofAsync remote.getOrderWithStatuses () OrdersLoaded raise

let update message model = 
    match message with
    | OrdersLoaded orders -> { MyOrders = Some orders }, Cmd.none


open Bolero.Html
type OrderList = Template<"wwwroot\MyOrders.html">

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
                            .Elt()
                    OrderList().Items(forEach orders viewOrder).Elt()
        ]
    ]
