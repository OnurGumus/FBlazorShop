namespace FBlazorShop.App.Model

open System.Collections.Generic
open System.Linq
open System
open Newtonsoft.Json


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
    Toppings : PizzaTopping list
}
with
    static member DefaultSize = 12
    static member MinimumSize = 9
    static member MaximumSize = 17
    member this.BasePrice = ((decimal)this.Size / (decimal)Pizza.DefaultSize) * this.Special.BasePrice
    member this.TotalPrice = this.BasePrice + this.Toppings.Sum(fun t -> t.Topping.Price)
    member this.FormattedTotalPrice =  this.TotalPrice.ToString("0.00");

[<CLIMutable>]
type Address ={
    Id :int
    Name:string
    Line1 : string
    Line2 : string
    City : string
    Region : string
    PostalCode : string
}
with
    static member Default = {
        Id = 0
        Name = ""
        Line1 = ""
        Line2 = ""
        City = ""
        Region = ""
        PostalCode = ""
    }
[<CLIMutable>]
type LatLong = {
    Latitude : double
    Longitude : double
}
with static member  Interpolate (start : LatLong) (endd : LatLong)  proportion = {
        Latitude = start.Latitude + (endd.Latitude - start.Latitude) * proportion;
        Longitude = start.Longitude + (endd.Longitude - start.Longitude) * proportion
    }

[<CLIMutable>]
type Order = {
    OrderId : int
    UserId : string
    CreatedTime : DateTime
    DeliveryAddress : Address
    DeliveryLocation : LatLong
    Pizzas : Pizza list
}
with
    member this.TotalPrice = this.Pizzas.Sum(fun p -> p.TotalPrice)
    member this.FormattedTotalPrice =  this.TotalPrice.ToString("0.00")

type OrderLight = {
    OrderId : int
    Pizzas : Pizza list
    UserId : string
    CreatedTime : DateTime
    DeliveryAddress : Address

}
type Marker = {
    Description : string
    X: double
    Y: double
    ShowPopup : bool
}
type OrderWithStatus ={
    Order : Order
    StatusText : string
    MapMarkers : Marker list
}
with
    static member PreparationDuration = TimeSpan.FromSeconds(10.0)
    static member DeliveryDuration = TimeSpan.FromMinutes(1.0)
    static member ToMapMarker description  coords showPopup =
        {
            Description = description
            X = coords.Longitude
            Y = coords.Latitude; ShowPopup = showPopup
        }

    static member  ComputeStartPosition( order : Order) =
        let rng = Random(order.OrderId)
        let distance = 0.01 + rng.NextDouble() * 0.02
        let angle = rng.NextDouble() * Math.PI * 2.0
        let offset = (distance * Math.Cos(angle)), (distance * Math.Sin(angle))
        {
            Latitude = order.DeliveryLocation.Latitude + (offset |> fst)
            Longitude = order.DeliveryLocation.Longitude + (offset |> snd)} : LatLong

    static member FromOrder (order : Order) =
        let dispatchTime = order.CreatedTime.Add(OrderWithStatus.PreparationDuration).ToLocalTime();
        let statusText, mapMarkers =
            if DateTime.Now < dispatchTime then
                "Preparing", [OrderWithStatus.ToMapMarker "You" order.DeliveryLocation true]
            elif DateTime.Now < dispatchTime + OrderWithStatus.DeliveryDuration then
                let startPosition = OrderWithStatus.ComputeStartPosition order

                let proportionOfDeliveryCompleted =
                    Math.Min(1.0, (DateTime.Now - dispatchTime).TotalMilliseconds / OrderWithStatus.DeliveryDuration.TotalMilliseconds)

                let driverPosition = LatLong.Interpolate startPosition order.DeliveryLocation proportionOfDeliveryCompleted

                "Out for delivery", [
                    OrderWithStatus.ToMapMarker "You" order.DeliveryLocation false
                    OrderWithStatus.ToMapMarker "Driver" driverPosition true]
            else
               "Delivered", [OrderWithStatus.ToMapMarker "Delivery location" order.DeliveryLocation true]
        {
            Order = order
            StatusText = statusText
            MapMarkers = mapMarkers}

