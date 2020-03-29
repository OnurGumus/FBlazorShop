module OrderReview

open FBlazorShop.App.Model
open Bolero
open Bolero.Html
type OrderDetail = Template<"wwwroot/OrderReview.html">

let view (order : Order) dispatch =

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
        .PizzaItems(forEach order.Pizzas viewPizzaItem)
        .FormattedTotalPrice(order.FormattedTotalPrice)
        .Elt()
