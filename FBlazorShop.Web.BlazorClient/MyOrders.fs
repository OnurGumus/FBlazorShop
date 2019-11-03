module MyOrders

open FBlazorShop.App.Model
open Elmish
open FBlazorShop.Web.BlazorClient.Services
open Bolero

type Model = { MyOrders : Order list option}

type Message =
    | OrdersLoaded  of Order list

let init (remote : PizzaService) () =
    { MyOrders = None } , Cmd.ofAsync remote.getOrders () OrdersLoaded raise

let update message model = 
    match message with
    | OrdersLoaded orders -> { MyOrders = Some orders }, Cmd.none

open Bolero.Html
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
                | Some orders -> text "Some orders"
        ]
    ]
//    <div class="main">
//    @if (ordersWithStatus == null)
//    {
//        <text>Loading...</text>
//    }
//    else if (ordersWithStatus.Count == 0)
//    {
//        <h2>No orders placed</h2>
//        <a class="btn btn-success" href="">Order some pizza</a>
//    }
//    else
//    {
//        <text>TODO: show orders</text>
//    }
//</div>