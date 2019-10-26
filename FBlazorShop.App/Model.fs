namespace FBlazorShop.App.Model

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
with member this.FormattedBasePrice =  this.BasePrice.ToString("0.00");
