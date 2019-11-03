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

let init (remote: PizzaService) =
    let pizzaConfigModel, pizzaConfigCmd = PizzaConfig.init remote ()
    let orderModel, orderCmd = Orders.init()
    let pizzaConfigCmd = Cmd.map PizzaConfigMsg pizzaConfigCmd
    let cmd = Cmd.ofAsync remote.getSpecials () SpecialsReceived raise
    let cmd = Cmd.batch [ cmd; pizzaConfigCmd; orderCmd ]
    { specials = []
      PizzaConfig = pizzaConfigModel
      Order = orderModel }, cmd

let update remote message model =
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
    //| OrderMsg (OrderAccepted _) ->
    //    let cmd = MyOrders.init remote () |> snd
    //    let init = { Model = {MyOrders = None } } : PageModel<MyOrders.Model>
    //    model, init |> MyOrders |> SetPage |> Cmd.ofMsg
    | OrderMsg msg ->
        let orderModel, cmd = Orders.update remote model.Order msg
        { model with Order = orderModel }, Cmd.map OrderMsg cmd

open Bolero
open BoleroHelpers

type PizzaCards = Template<"wwwroot\PizzaCards.html">

type ViewItem() =
    inherit ElmishComponent<PizzaSpecial, Message>()

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

let view model dispatch =
    let pizzaconfig = PizzaConfig.view model.PizzaConfig (PizzaConfigMsg >> dispatch)

    cond model.specials <| function
    | [] -> empty
    | _ ->
        let orderContents = Orders.view model.Order (OrderMsg >> dispatch)
        PizzaCards()
            .Items(forEach model.specials <| fun i -> ecomp<ViewItem, _, _> i dispatch)
            .OrderContents(orderContents)
            .PizzaConfig(pizzaconfig)
            .Elt()
