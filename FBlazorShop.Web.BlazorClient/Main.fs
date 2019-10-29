module FBlazorShop.Web.BlazorClient.Main

open Microsoft.AspNetCore.Components.Routing
open Microsoft.JSInterop
open FBlazorShop.App.Model
open Bolero.Remoting
open Elmish
open Services
open PizzaConfig
open Orders
open Bolero

type Page =
   
    | [<EndPoint "/">] Home                    
    | [<EndPoint "/myOrders">] MyOrders 

type Model = { 
    specials: PizzaSpecial list
    PizzaConfig : PizzaConfig.Model
    Order : Orders.Model
    Page : Page
}
type Message = 
    | SpecialsReceived of PizzaSpecial list 
    | PizzaConfigMsg of PizzaConfigMsg
    | OrderMsg of OrderMsg
    | SetPage of Page


let router = Router.infer SetPage (fun (m : Model) -> m.Page)

let initModel (remote : PizzaService) =
    let pizzaConfigModel , pizzaConfigCmd =  PizzaConfig.init remote ()
    let orderModel, orderCmd = Orders.init()
    let pizzaConfigCmd = Cmd.map  PizzaConfigMsg pizzaConfigCmd
    let cmd = Cmd.ofAsync remote.getSpecials () SpecialsReceived raise
    let cmd = Cmd.batch [ cmd ; pizzaConfigCmd]
    { specials = []; PizzaConfig = pizzaConfigModel; Order = orderModel; Page = Home }, cmd
    

let update remote message model =
    match message with
    | SetPage page -> { model with Page = page }, Cmd.none
    | SpecialsReceived d -> { model with specials = d }, Cmd.none
    | PizzaConfigMsg (ConfigDone p ) ->  model, Cmd.ofMsg (p |> PizzaAdded |> OrderMsg)
    | PizzaConfigMsg msg -> 
        let pizzaConfigModel, cmd = PizzaConfig.update model.PizzaConfig msg
        {model with PizzaConfig = pizzaConfigModel}, Cmd.map PizzaConfigMsg cmd
    | OrderMsg msg ->
        let orderModel, cmd =  Orders.update model.Order msg
        {model with Order = orderModel}, Cmd.map OrderMsg cmd
        
    
open Bolero.Html
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
        cond model.Page <| function
        | Home ->
            cond model.specials <| function
            | [] -> empty
            | _ ->
                let orderContents = Orders.view model.Order  (OrderMsg >> dispatch )
                PizzaCards()
                    .Items(forEach model.specials <| fun i ->
                        ecomp<ViewItem,_,_> i dispatch)
                    .OrderContents(orderContents)
                    .Elt()
        | MyOrders -> empty
    
    let pizzaconfig = PizzaConfig.view model.PizzaConfig (PizzaConfigMsg >> dispatch)
    MainLayout()
        .GetPizzaLink(navLink NavLinkMatch.All 
            [attr.href "/"; attr.``class`` "nav-tab"] 
            [
                img [attr.src ("img/pizza-slice.svg" |> prependContent)] 
                div [] [text "Get Pizza"]
            ])
        .MyOrders(navLink NavLinkMatch.All 
            [attr.href "myOrders"; attr.``class`` "nav-tab"] 
            [
                img [attr.src ("img/bike.svg" |> prependContent)] 
                div [] [text "My Orders"]
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
        |> Program.withRouter router
#if DEBUG

        |> Program.withConsoleTrace
        |> Program.withErrorHandler (printf "%A")
        |> Program.withHotReload
#endif

//type MyComponent() =
//    inherit Component()

//    override this.BuildRenderTree(builder) =
//        builder.OpenComponent<Router>(0)