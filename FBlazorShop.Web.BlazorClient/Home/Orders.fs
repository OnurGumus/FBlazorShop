module Orders

open FBlazorShop.App.Model
open Elmish
open Bolero.Html
open FBlazorShop.Web.BlazorClient
open Services
open System
open Microsoft.JSInterop
open Newtonsoft.Json
open Bolero

type Model = { Order : Order option; }

type OrderMsg =
    | PizzaAdded of Pizza
    | PizzaRemoved of int
    | CheckoutRequested of Order
    | PizzasLoaded of Pizza list
    | StorageUpdated
    | Rendered


let getPizzas (jsRuntime : IJSRuntime)  =
    let doWork () =
        async{
            let! res =
                jsRuntime.InvokeAsync<string>("window.localStorage.getItem", "pizzas")
                    .AsTask()
                    |> Async.AwaitTask
            return
                match res with
                | null -> PizzasLoaded []
                | t -> t |> JsonConvert.DeserializeObject<Pizza list> |> PizzasLoaded
        }
    Cmd.ofAsync doWork () id (fun _ -> PizzasLoaded [])

let updatePizzaList (jsRuntime : IJSRuntime)  pizzas =
    let doWork () =
        async{
            let pizzaSer = JsonConvert.SerializeObject(pizzas)
            do!
                jsRuntime.InvokeVoidAsync("window.localStorage.setItem", "pizzas", pizzaSer)
                    .AsTask()
                    |> Async.AwaitTask
            return StorageUpdated
        }
    Cmd.ofAsync doWork () id raise
let init jsRuntime =
    { Order = None; }, Cmd.none

let update (remote : PizzaService)  (jsRuntime : IJSRuntime) ( state : Model) (msg : OrderMsg) : Model * Cmd<_> =
    match msg with
    | Rendered -> state , getPizzas jsRuntime
    | StorageUpdated -> state, Cmd.none
    | PizzasLoaded pizzas -> state, Cmd.batch(pizzas |> List.map(PizzaAdded >> Cmd.ofMsg))
    | PizzaAdded p ->
        let order =
            match state.Order with
            | Some order -> { order with Pizzas = p :: [yield! order.Pizzas] } |> Some
            | _ ->
                {
                    OrderId = Guid.NewGuid().ToString()
                    UserId = ""
                    CreatedTime = System.DateTime.Now
                    DeliveryAddress = Address.Default
                    DeliveryLocation = { Latitude = 51.5001 ; Longitude = -0.1239}
                    Pizzas = [p];
                    Version = 0;
                    DeliveryStatus = NotDelivered;
                    CurrentLocation =  { Latitude = 51.5001 ; Longitude = -0.1239}
                } |> Some

        { Order = order }, updatePizzaList jsRuntime order.Value.Pizzas


    | PizzaRemoved tobeRemoved ->
        let pizzas =
            [yield! state.Order.Value.Pizzas]
            |> List.indexed
            |> List.filter (fun (i,_) -> i <> tobeRemoved)
            |> List.map snd

        let cmd = updatePizzaList jsRuntime pizzas
        if pizzas.Length = 0 then
            {state with Order = None}, cmd
        else
        let order =
                { state.Order.Value with Pizzas = pizzas}
        { state with Order = Some order}, cmd

    | CheckoutRequested _ -> invalidOp "should not happen"


type OrderView() =
    inherit ElmishComponent<Model, OrderMsg>()
    override this.OnAfterRenderAsync(firstRender) =
        let res = base.OnAfterRenderAsync(firstRender) |> Async.AwaitTask
        async{
            do! res
            if firstRender then
                this.Dispatch Rendered
            return ()
         }|> Async.StartImmediateAsTask :> _

    override _.View state dispatcher =
        let noOrder =
            div [attr.``class`` "empty-cart"] [
                text "Choose a pizza"; br[]; text "to get started"
            ]

        let cartItem (index, pizza : Pizza) =
            div [attr.``class`` "cart-item"] [
                a [on.click (fun _ ->   index |> PizzaRemoved |> dispatcher); attr.``class`` "delete-item"] [text "x"]
                div [attr.``class`` "title"] [textf "%s\" %s" (pizza.Size.ToString()) pizza.Special.Name]
                ul [][
                    forEach pizza.Toppings (fun t -> li [] [textf "+%s" t.Topping.Name])
                ]
                div [attr.``class`` "item-price"][
                    text pizza.FormattedTotalPrice
                ]
            ]

        let upper =
            cond state.Order <| function
            | Some o ->
                cond (o.Pizzas.Length = 0) <| function
                | true -> noOrder
                | _ ->
                    div [attr.``class`` "order-contents"][
                        h2 [] [text "Your order"]
                        forEach (o.Pizzas |> Seq.indexed) cartItem
                    ]
            | _ -> noOrder

        let lower =
            cond state.Order <| function
            | Some order ->
                div [attr.``class`` "order-total" ][
                    text "Total:"
                    span [attr.``class`` "total-price"] [text (order.FormattedTotalPrice)]
                    button [attr.``class`` "btn btn-warning"; on.click (fun _ -> order |> CheckoutRequested |> dispatcher)][ text "Order >"]
                ]
            | _ -> empty

        concat [ upper; lower]


let view (state : Model) dispatcher =
    ecomp<OrderView,_,_> [] state dispatcher