module Checkout


open FBlazorShop.App.Model
open Elmish
open FBlazorShop.Web.BlazorClient.Services
open Bolero

type Model = { 
    Order : Order option
    CurrentAddress : Address
    ValidatedAddress : Result<Address,Map<string,string list>> option 
}

open Validation

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
    | OrderAccepted of orderId : int
    | SetAddressName of string
    | SetAddressCity of string
    | SetAddressLine1 of string
    | SetAddressLine2 of string
    | SetAddressRegion of string
    | SetAddressPostalCode of string

let init (_ : PizzaService) (order : Order option)  =
    { Order =  order ; CurrentAddress = Address.Default; ValidatedAddress = None } , Cmd.none

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
        
    match (message, (model,commonState)) with
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
          
    | _ , ({ ValidatedAddress = Some(Error _) } ,_) -> model |> noCommand

    | OrderPlaced order, ({ ValidatedAddress = None } , _) -> 
        model.CurrentAddress |> validateModelForAddressForced, Cmd.ofMsg (OrderPlaced order) , Cmd.none
    | OrderPlaced order, (_, { Authentication = None}) ->  
        let c = Cmd.ofMsg(OrderPlaced order)
        model, c, Common.authenticationRequested
    | OrderPlaced order, _ -> 
        let order  = {order with DeliveryAddress = model.CurrentAddress}
        let cmd = Cmd.ofAsync remote.placeOrder order OrderAccepted raise
        model, cmd, Cmd.none

    | OrderAccepted _ , _ -> invalidOp "should not happen"

open Bolero.Html
open System

type OrderDetail = Template<"wwwroot\Checkout.html">
let view (model : Model) dispatch = 
    div [ attr.``class`` "main"][
    cond model.Order <| function
        | Some currentOrder -> 
            let pd f = Action<_> (fun n -> n |> f |> dispatch)
            let errorAndClass name (result:Result<_,Map<string, string list>> option) = 
                match result with
                | Some (Error e) when (e.ContainsKey name && e.[name] <> []) -> String.Join(",", e.[name]), "invalid"
                | Some _ -> "", "modified valid"
                | None -> "",""

            let formFieldItem (name : string) value =
                let error, validClass = errorAndClass name model.ValidatedAddress
                OrderDetail
                    .FormFieldItem()
                        .Label(name)
                        .ValidClass(validClass)
                        .Value(value)
                        .Error(error)
                        .Elt()

            let address = model.CurrentAddress
            
            let formItems = 
                concat [ 
                    formFieldItem (nameof address.Name) (address.Name, pd SetAddressName)
                    formFieldItem (nameof address.City) (address.City, pd SetAddressCity)
                    formFieldItem (nameof address.Region) (address.Region, pd SetAddressRegion)
                    formFieldItem (nameof address.PostalCode) (address.PostalCode, pd SetAddressPostalCode)
                    formFieldItem (nameof address.Line1) (address.Line1, pd SetAddressLine1)
                    formFieldItem (nameof address.Line2) (address.Line2, pd SetAddressLine2)
                ]

            OrderDetail()
                .OrderReview(OrderReview.view currentOrder dispatch)
                .PlaceOrder(fun _ -> dispatch (OrderPlaced currentOrder))
                .FormFieldItems(formItems)
                .Elt()
           
        | _ -> text "Loading..."
    ]