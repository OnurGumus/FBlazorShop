module Checkout


open FBlazorShop.App.Model
open Elmish
open FBlazorShop.Web.BlazorClient.Services
open Bolero

type Model = {
    Order : Order option
    CurrentAddress : Address
    ValidatedAddress : Result<Address,Map<string,string list>> option
    Focus : string option
    OrderPlaced : bool
}

open Validation
open System

let validateAddress (address : Address) =
    let cannotBeBlank (validator:Validator<string>) name value =
        validator.Test name value
        |> validator.NotBlank (name + " cannot be blank") |> validator.End
    all <| fun t -> {
        Id = 0
        Name = cannotBeBlank t (nameof address.Name) address.Name
        Line1 =  cannotBeBlank t (nameof address.Line1) address.Line1
        Line2 =  cannotBeBlank t (nameof address.Line2) address.Line2
        City =  cannotBeBlank t (nameof address.City)  address.City
        Region =  cannotBeBlank t (nameof address.Region) address.Region
        PostalCode =  cannotBeBlank t (nameof address.PostalCode) address.PostalCode
    }



type Message =
    | OrderPlaced of Order : Order
    | OrderAccepted of orderId : string
    | SetAddressName of string
    | SetAddressCity of string
    | SetAddressLine1 of string
    | SetAddressLine2 of string
    | SetAddressRegion of string
    | SetAddressPostalCode of string
    | Focused of  string

let init (_ : PizzaService) (order : Order option)  =
    {   Order =  order ;
        CurrentAddress = Address.Default;
        ValidatedAddress = None ;
        Focus = None
        OrderPlaced = false
    } , Cmd.none

let noCommand state =  state, Cmd.none, Cmd.none
let update remote message (model : Model, commonState : Common.State) =

    let validateModelForAddressForced address =
        let vAddress = validateAddress address
        {model with CurrentAddress = address; ValidatedAddress = Some vAddress}

    let validateModelForAddress address =
        match model.ValidatedAddress with
        | None  ->
            {model with CurrentAddress = address;}
        | Some _ -> validateModelForAddressForced address
    let model = { model with Focus = None}
    match (message, (model,commonState.Authentication)) with
    | Focused field, _ -> { model with Focus = Some field} |> noCommand
    | SetAddressName value, _ ->
      { model.CurrentAddress with Name = value}
      |> validateModelForAddress
      |> noCommand

    | SetAddressCity value, _ ->
        { model.CurrentAddress with City = value}
        |> validateModelForAddress
        |> noCommand

    | SetAddressLine1 value, _ ->
        { model.CurrentAddress with Line1 = value}
        |> validateModelForAddress
        |> noCommand

    | SetAddressLine2 value, _ ->
        { model.CurrentAddress with Line2 = value}
        |> validateModelForAddress
        |> noCommand

    | SetAddressPostalCode value, _ ->
        { model.CurrentAddress with PostalCode = value}
        |> validateModelForAddress
        |> noCommand

    | SetAddressRegion value, _ ->
        { model.CurrentAddress with Region = value}
        |> validateModelForAddress
        |> noCommand
    | OrderPlaced _, ({ OrderPlaced = true } , _)
    | _ , ({ ValidatedAddress = Some(Error _) } ,_) -> model |> noCommand

    | OrderPlaced order, ({ ValidatedAddress = None } , _) ->
        model.CurrentAddress
        |> validateModelForAddressForced, Cmd.ofMsg (OrderPlaced order) , Cmd.none
    | OrderPlaced order, (_, Common.AuthState.Failed) ->
        let c = Cmd.ofMsg(OrderPlaced order)
        model, c, Common.authenticationRequested
    | OrderPlaced order,(_,Common.AuthState.Success auth) ->
        let order  = {order with DeliveryAddress = model.CurrentAddress}
        let cmd = Cmd.ofAsync remote.placeOrder (auth.Token, order) OrderAccepted raise
        { model with OrderPlaced = true} , cmd, Cmd.none
    | _, (_, Common.AuthState.NotTried)
    | OrderAccepted _ , _ -> invalidOp "should not happen"

open Bolero.Html
open System

type Checkout = Template<"wwwroot\Checkout.html">

let view (model : Model) dispatch =
    div [ attr.``class`` "main"][
    cond model.Order <| function
        | Some currentOrder ->
            let pd f = Action<_> (fun n -> n |> f |> dispatch)
            let focused = (fun name -> Action<_>(fun _ -> dispatch (Focused name)))
            let address = model.CurrentAddress
            let formFieldItem = BoleroHelpers.formFieldItem model.ValidatedAddress model.Focus focused  "text"
            let formItems =
                concat [
                    formFieldItem (nameof address.Name) (address.Name, pd SetAddressName)
                    formFieldItem (nameof address.City) (address.City, pd SetAddressCity)
                    formFieldItem (nameof address.Region) (address.Region, pd SetAddressRegion)
                    formFieldItem (nameof address.PostalCode) (address.PostalCode, pd SetAddressPostalCode)
                    formFieldItem (nameof address.Line1) (address.Line1, pd SetAddressLine1)
                    formFieldItem (nameof address.Line2) (address.Line2, pd SetAddressLine2)
                ]

            Checkout()
                .OrderReview(OrderReview.view currentOrder dispatch)
                .PlaceOrder(fun _ -> dispatch (OrderPlaced currentOrder))
                .FormFieldItems(formItems)
                .Elt()

        | _ -> text "Loading..."
    ]