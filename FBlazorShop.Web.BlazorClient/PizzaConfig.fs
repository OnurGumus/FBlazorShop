module PizzaConfig

open FBlazorShop.App.Model
open Elmish
open Bolero.Html

type Model = Pizza option

type PizzaConfigMsg= 
    | PizzaConfigRequested of PizzaSpecial

let init () = None


let update ( state : Model) (msg : PizzaConfigMsg) : Model * Cmd<_> = 
    match msg with
    | PizzaConfigRequested p -> 
        { 
             Id = 0; 
             OrderId = 0;  
             Special = p; 
             SpecialId =p.Id; 
             Size = Pizza.DefaultSize
             Toppings = [] 
        } |> Some,
        Cmd.none

open Bolero

type PizzaConfig = Template<"wwwroot\ConfigurePizza.html">

let view (model : Model) dispatcher = 
    match model with
    | Some pizza ->
        PizzaConfig()
            .SpecialName(pizza.Special.Name)
            .FormattedTotalPrice(pizza.FormattedTotalPrice)
            .SpecialDescription(pizza.Special.Description)
            .Elt()
    | _ -> empty
