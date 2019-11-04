module OrderDetail

open FBlazorShop.App.Model
open Elmish
open FBlazorShop.Web.BlazorClient.Services
open Bolero

type Model = { Order : OrderWithStatus option}

//let startTimer =
//       let sub dispatch =
//            if !!(window?myInterval) |> isNull then
//               let interval = window.setInterval  ((fun () -> dispatch Tick), 10, [])
//               window?myInterval <- interval
//       Cmd.ofSub sub

//let stopTimer : Cmd<Message>=
//       let sub _ =
//           window.clearInterval !!(window?myInterval)
//           window?myInterval <- null
//       Cmd.ofSub sub
type Message =
    | OrderLoaded of id :int * OrderWithStatus option

let loadPeriodically remote id =
    let doWork i = 
        async{ 
            do! Async.Sleep 4000; 
            return! remote.getOrderWithStatus i 
    }
    Cmd.ofAsync doWork id (fun m -> OrderLoaded(id,m)) raise

let init (remote : PizzaService) id  =
    { Order = None } , Cmd.ofAsync remote.getOrderWithStatus id (fun m -> OrderLoaded(id,m)) raise

let update remote message (model : Model) = 
    match message with
    | OrderLoaded (id, order) -> { Order =  order }, loadPeriodically remote id

open Bolero.Html

type OrderDetail = Template<"wwwroot\OrderDetail.html">
let view (model : Model) dispatch = 
    div [ attr.``class`` "main"][
    cond model.Order <| function
        | Some x -> 
            let viewTopping (topping : PizzaTopping) =
                OrderDetail
                    .ToppingItem()
                    .ToppingName(topping.Topping.Name)
                    .Elt()

            let viewPizzaItem (pizza : Pizza) =
                OrderDetail.PizzaItem()
                    .FormattedTotalPrice(pizza.FormattedTotalPrice)
                    .Size(pizza.Size.ToString())
                    .SpecialName(pizza.Special.Name)
                    .ToppingItems(forEach pizza.Toppings viewTopping)
                    .Elt()
                    
            OrderDetail()
                .OrderCreatedTimeToLongDateString(x.Order.CreatedTime.ToLongDateString())
                .PizzaItems(forEach x.Order.Pizzas viewPizzaItem)
                .StatusText(x.StatusText)
                .FormattedTotalPrice(x.Order.FormattedTotalPrice)
                .Elt()
           
        | _ -> text "Loading..."
    ]