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
    | [<EndPoint "/">] Home   of Model : PageModel<Home.Model>                  
    | [<EndPoint "/myOrders">] MyOrders of Model : PageModel<MyOrders.Model>

type Model = { 
    Page : Page
}

type Message = 
    | SetPage of Page
    | HomeMsg of Home.Message
    | MyOrdersMsg of MyOrders.Message

let defaultPageModel remote = function
| MyOrders m -> Router.definePageModel m (MyOrders.init remote () |> fst)
| Home m ->Router.definePageModel m (Home.init remote |> fst)

let router remote = Router.inferWithModel SetPage (fun (m : Model) -> m.Page) (defaultPageModel remote)
//let initModel (remote : PizzaService) =
//    let pizzaConfigModel , pizzaConfigCmd =  PizzaConfig.init remote ()
//    let orderModel, orderCmd = Orders.init()
//    let pizzaConfigCmd = Cmd.map  PizzaConfigMsg pizzaConfigCmd
//    let cmd = Cmd.ofAsync remote.getSpecials () SpecialsReceived raise
//    let cmd = Cmd.batch [ cmd ; pizzaConfigCmd; orderCmd]
//    { specials = []; PizzaConfig = pizzaConfigModel; Order = orderModel; Page = Home }, cmd

let init remote  = 
    let homeModel, homeCmd = Home.init remote
    let home = { Model = homeModel  } |> Home
    {Page = home },  Cmd.map HomeMsg homeCmd

let update remote message (model : Model) =
    match message, model.Page with
    | SetPage(Home _  ), _ -> init remote
    | SetPage(MyOrders _ as page),_ -> { model with Page = page }, Cmd.none
    | HomeMsg msg, Home homeModel ->
        let homeModel, cmd = Home.update remote msg homeModel.Model
        {model with Page = Home({ Model = homeModel})}, Cmd.map HomeMsg cmd
    | MyOrdersMsg msg, MyOrders myOrdersModel ->
        let myOrdersModel, cmd = MyOrders.update  msg myOrdersModel.Model
        {model with Page = MyOrders({ Model = myOrdersModel})}, Cmd.map HomeMsg cmd
    | _ -> failwith "not supported"
    //| SpecialsReceived d -> { model with specials = d }, Cmd.none
    //| PizzaConfigMsg (ConfigDone p ) ->  model, Cmd.ofMsg (p |> PizzaAdded |> OrderMsg)
    //| PizzaConfigMsg msg -> 
    //    let pizzaConfigModel, cmd = PizzaConfig.update model.PizzaConfig msg
    //    {model with PizzaConfig = pizzaConfigModel}, Cmd.map PizzaConfigMsg cmd
    //| OrderMsg (OrderAccepted _) -> 
    //    let cmd = MyOrders.init remote () |> snd
    //    let init = { Model = {MyOrders = None } } : PageModel<MyOrders.Model>
    //    model, init |> MyOrders |> SetPage |> Cmd.ofMsg
    //| OrderMsg msg ->
    //    let orderModel, cmd =  Orders.update remote model.Order msg
        //{model with Order = orderModel}, Cmd.map OrderMsg cmd
    //| MyOrderMsg msg ->
    //    let orderModel, cmd = MyOrders.update msg model.Order 
    //    {model with Page = MyOrders ( model.Page.)}, Cmd.map MyOrderMsg cmd
        
    
open Bolero.Html
open BoleroHelpers

type MainLayout = Template<"wwwroot\MainLayout.html">


let view ( model : Model) dispatch =
    let content =
        cond model.Page <| function
        | Home (model) ->
          Home.view model.Model (HomeMsg >> dispatch)
        | MyOrders model -> MyOrders.view model.Model (MyOrdersMsg >> dispatch)
    
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
        .Elt()

open Bolero.Templating.Client


type MyApp() =
    inherit ProgramComponent<Model, Message>()

    override this.Program =
        let remote = this.Remote<PizzaService>()
        let update = update remote
        Program.mkProgram (fun _ -> init remote ) update view
        |> Program.withRouter (router remote)
#if DEBUG

        |> Program.withConsoleTrace
        |> Program.withErrorHandler (printf "%A")
        |> Program.withHotReload
#endif

//type MyComponent() =
//    inherit Component()

//    override this.BuildRenderTree(builder) =
//        builder.OpenComponent<Router>(0)