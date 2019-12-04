module Home

open Orders
open PizzaConfig

open FBlazorShop.App.Model
open FBlazorShop.Web.BlazorClient.Services

type Model =
    { specials: PizzaSpecial list
      PizzaConfig: PizzaConfig.Model
      Order: Orders.Model }


type Message =
    | SpecialsReceived of PizzaSpecial list
    | PizzaConfigMsg of PizzaConfigMsg
    | OrderMsg of OrderMsg

open Elmish

let init (remote: PizzaService) jsRuntime =
    let pizzaConfigModel, pizzaConfigCmd = PizzaConfig.init remote ()
    let orderModel, orderCmd = Orders.init jsRuntime
    let pizzaConfigCmd = Cmd.map PizzaConfigMsg pizzaConfigCmd
    let cmd = Cmd.ofAsync remote.getSpecials () SpecialsReceived raise
    let cmd = Cmd.batch [ cmd; pizzaConfigCmd; Cmd.map OrderMsg orderCmd  ]
    { specials = []
      PizzaConfig = pizzaConfigModel
      Order = orderModel }, cmd

let update remote jsRuntime message model =
    match message with
    | SpecialsReceived d -> { model with specials = d }, Cmd.none
    | PizzaConfigMsg(ConfigDone p) ->
        model,
        Cmd.ofMsg
            (p
             |> PizzaAdded
             |> OrderMsg)
    | PizzaConfigMsg msg ->
        let pizzaConfigModel, cmd = PizzaConfig.update model.PizzaConfig msg
        { model with PizzaConfig = pizzaConfigModel }, Cmd.map PizzaConfigMsg cmd

    | OrderMsg msg ->
        let orderModel, cmd = Orders.update  remote jsRuntime model.Order msg
        { model with Order = orderModel }, Cmd.map OrderMsg cmd

open Bolero
open BoleroHelpers

type PizzaCards = Template<"wwwroot\PizzaCards.html">

type ViewItem() =
    inherit ElmishComponent<PizzaSpecial, Message>()
    // Check for model changes by only looking at the value.
    override _.ShouldRender(oldModel, newModel) =
        oldModel.Id <> newModel.Id

    override __.View special dispatch =

        PizzaCards.Item()
            .description(special.Description)
            .imageurl(special.ImageUrl |> prependContent)
            .name(special.Name)
            .price(special.FormattedBasePrice)
            .specialSelected(fun _ ->
                  special
                  |> PizzaConfigRequested
                  |> PizzaConfigMsg
                  |> dispatch).Elt()
open Bolero.Html

type Cards() =
    inherit ElmishComponent<Model, Message>()
    // Check for model changes by only looking at the value.
    override _.ShouldRender(oldModel, newModel) =
        //oldModel.specials <> newModel.specials
        false
    override __.View model dispatch =
        forEach model.specials <| fun i -> ecomp<ViewItem, _, _> i dispatch


let view (model:Model) dispatch =
    cond (model |> box |> isNull) <| function
    | true -> h2  [] [text "Loading data, please wait..."]
    | _ ->
        cond model.specials <| function
        | [] -> h2  [] [text "Loading data, please wait..."]
        | _ ->
         let cards = ecomp<Cards, _, _> model dispatch
         let pizzaconfig = PizzaConfig.view model.PizzaConfig (PizzaConfigMsg >> dispatch)
         let orderContents = Orders.view model.Order (OrderMsg >> dispatch)
         PizzaCards()
             .Items(cards)
             .OrderContents(orderContents)
             .PizzaConfig(pizzaconfig)
             .Elt()
