namespace FBlazorShop.App.Model

open System.Collections.Generic
open System.Linq

/// <summary>
/// Represents a pre-configured template for a pizza a user can order
/// </summary>
[<CLIMutable>]
type PizzaSpecial = {
    Id : int
    Name : string
    BasePrice : decimal
    Description : string
    ImageUrl : string
}
with member this.FormattedBasePrice =  this.BasePrice.ToString("0.00")

[<CLIMutable>]
type Topping = {
    Id : int
    Name : string
    Price : decimal
}
with member this.FormattedBasePrice = this.Price.ToString("0.00")

[<CLIMutable>]
type PizzaTopping = {
    Topping : Topping;
    ToppingId : int
    PizzaId : int
}

[<CLIMutable>]
type Pizza = {
    Id : int
    OrderId : int
    Special : PizzaSpecial
    SpecialId : int
    Size : int
    Toppings : IReadOnlyList<PizzaTopping>
}
with 
    static member DefaultSize = 12
    static member MinimumSize = 9
    static member MaximumSize = 17
    member this.BasePrice = ((decimal)this.Size / (decimal)Pizza.DefaultSize) * this.Special.BasePrice
    member this.TotalPrice = this.BasePrice + this.Toppings.Sum(fun t -> t.Topping.Price)
    member this.FormattedTotalPrice =  this.TotalPrice.ToString("0.00");
