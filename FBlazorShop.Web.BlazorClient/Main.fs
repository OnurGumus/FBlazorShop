module FBlazorShop.Web.BlazorClient.Main

open Microsoft.AspNetCore.Components.Routing
open Microsoft.JSInterop
open FBlazorShop.App.Model
open Bolero.Remoting
open Elmish
open Services
open PizzaConfig
open Orders

type Model = { specials: PizzaSpecial list; PizzaConfig : PizzaConfig.Model; Order : Orders.Model}
type Message = 
    | SpecialsReceived of PizzaSpecial list 
    | PizzaConfigMsg of PizzaConfigMsg
    | OrderMsg of OrderMsg

let initModel (remote : PizzaService) =
    let pizzaConfigModel , pizzaConfigCmd =  PizzaConfig.init remote ()
    let orderModel, orderCmd = Orders.init()
    let pizzaConfigCmd = Cmd.map  PizzaConfigMsg pizzaConfigCmd
    let cmd = Cmd.ofAsync remote.getSpecials () SpecialsReceived raise
    let cmd = Cmd.batch [ cmd ; pizzaConfigCmd]
    { specials = []; PizzaConfig = pizzaConfigModel; Order = orderModel }, cmd
    

let update remote message model =
    match message with
    | SpecialsReceived d -> { model with specials = d }, Cmd.none
    | PizzaConfigMsg (ConfigDone p ) ->  model, Cmd.ofMsg (p |> PizzaAdded |> OrderMsg)
    | PizzaConfigMsg msg -> 
        let pizzaConfigModel, cmd = PizzaConfig.update model.PizzaConfig msg
        {model with PizzaConfig = pizzaConfigModel}, Cmd.map PizzaConfigMsg cmd
    | OrderMsg msg ->
        let orderModel, cmd =  Orders.update model.Order msg
        {model with Order = orderModel}, Cmd.map OrderMsg cmd
        
    
open Bolero.Html
open Bolero
open BoleroHelpers

type MainLayout = Template<"wwwroot\MainLayout.html">
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
                |> dispatch)
            .Elt()

let view ( model : Model) dispatch =
    let content =
        cond model.specials <| function
        | [] -> empty
        | _ ->
            let orderContents = Orders.view model.Order  (OrderMsg >> dispatch )
            PizzaCards()
                .Items(forEach model.specials <| fun i ->
                    ecomp<ViewItem,_,_> i dispatch)
                .OrderContents(orderContents)
                .Elt()
    
    let pizzaconfig = PizzaConfig.view model.PizzaConfig (PizzaConfigMsg >> dispatch)
    MainLayout()
        .GetPizzaLink(navLink NavLinkMatch.All 
            [attr.href ""; attr.``class`` "nav-tab"] 
            [
                img [attr.src ("img/pizza-slice.svg" |> prependContent)] 
                div [] [text "Get Pizza"]
            ])
        .Body(content)
        .PizzaConfig(pizzaconfig)
        
        .Elt()

open Bolero.Templating.Client

type MyApp() =
    inherit ProgramComponent<Model, Message>()

    override this.Program =
        let remote = this.Remote<PizzaService>()
        let update = update remote
        let init = initModel remote
        Program.mkProgram (fun _ -> init) update view
#if DEBUG

        |> Program.withConsoleTrace
        |> Program.withErrorHandler (printf "%A")
        |> Program.withHotReload
#endif