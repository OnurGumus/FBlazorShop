module PizzaConfig

open FBlazorShop.App.Model
open Elmish
open Bolero.Html
open FBlazorShop.Web.BlazorClient
open Services
open System

type Model = { Pizza : Pizza option; Toppings : Topping list }

type PizzaConfigMsg= 
    | PizzaConfigRequested of PizzaSpecial
    | SizeUpdated of int
    | ToppingsReceived of Topping list
    | ToppingSelected of int

let init (remote : PizzaService ) () = 
    let cmd = Cmd.ofAsync remote.getToppings () ToppingsReceived raise
    { Pizza = None; Toppings = [] }, cmd


let update ( state : Model) (msg : PizzaConfigMsg) : Model * Cmd<_> = 
    match msg with
    | PizzaConfigRequested p -> 
        { state 
            with Pizza = { 
                     Id = 0; 
                     OrderId = 0;  
                     Special = p; 
                     SpecialId =p.Id; 
                     Size = Pizza.DefaultSize
                     Toppings = [] 
                } |> Some 
        },
        Cmd.none
    | SizeUpdated i ->
        { state with Pizza = { state.Pizza.Value with Size = i } |> Some }, Cmd.none
    | ToppingsReceived toppings -> { state with Toppings = toppings } , Cmd.none
    | ToppingSelected t -> 
        let pizza = state.Pizza.Value
        let topping = state.Toppings.Item t
        let pizzaTopping = { Topping = topping; ToppingId = topping.Id; PizzaId = pizza.Id }
        let toppings = pizzaTopping :: (pizza.Toppings |> List.ofSeq)
        let pizza = { pizza with Toppings = toppings} |> Some
        { state with Pizza = pizza }, Cmd.none

open Bolero

let viewToppings (pizza : Pizza) (toppings : Topping list) dispatcher = 
    div [] [
        label [] [ text "Extra Toppings:"]
        let length = toppings.Length
        cond (length = 0)  <| function
            | true ->
                select [attr.``class`` "custom-select"; attr.disabled true] [
                    option [] [text "(loading...)"]
                ]
            | _ ->
                cond (pizza.Toppings.Count >= 6) <| function 
                | true -> div [] [text "(maximum reached)"]
                | _ ->
                    select [attr.``class`` "custom-select" ; on.change (fun e -> e.Value |> Convert.ToInt32 |> ToppingSelected |> dispatcher )] [
                        option [ attr.disabled true; attr.selected true; attr.value -1] [text "select"]
                        forEach (toppings |> List.mapi (fun i t -> (i,t))) ( fun (i,t) -> option [ attr.value i] [textf "%s - $%s" t.Name t.FormattedBasePrice] )
                    ]
                
        //        <select class="custom-select" disabled>
        //        <option>(loading...)</option>
        //</select>
    ]
type PizzaConfig = Template<"wwwroot\ConfigurePizza.html">
open System.Collections.Generic

let viewToppingItems (toppings : IReadOnlyList<PizzaTopping> ) =
    forEach toppings (fun t -> PizzaConfig.ToppingItem().Name(t.Topping.Name).FormattedPrice(t.Topping.FormattedBasePrice).Elt())

let view (model : Model) dispatcher = 
    match model.Pizza with
    | Some pizza ->
        let toppings = viewToppings pizza (model.Toppings) dispatcher
        let toppingItems = viewToppingItems pizza.Toppings 
        PizzaConfig()
            .ToppingItems(toppingItems)
            .ToppingCombo(toppings)
            .SpecialName(pizza.Special.Name)
            .FormattedTotalPrice(pizza.FormattedTotalPrice)
            .SpecialDescription(pizza.Special.Description)
            .MaximumSize(Pizza.MaximumSize)
            .MinimumSize(Pizza.MinimumSize)
            .Size(pizza.Size.ToString())
            .SizeN(pizza.Size.ToString(), fun i ->  SizeUpdated (System.Int32.Parse (i)) |> dispatcher)
            .Elt()
    | _ -> empty
