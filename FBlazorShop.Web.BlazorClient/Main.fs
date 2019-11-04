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
| Home m ->Router.definePageModel m (Home.init remote () |> fst)

let router remote = Router.inferWithModel SetPage (fun (m : Model) -> m.Page) (defaultPageModel remote)

let inline initPage init msg page =
    let model, cmd = init ()
    let page = { Model = model  } |> page
    {Page = page }, Cmd.map msg cmd

let initMyOrders remote = 
    initPage (MyOrders.init remote) MyOrdersMsg MyOrders

let initHome remote = 
    initPage (Home.init remote) HomeMsg Home
    
let init remote () = initHome remote

let update remote message (model : Model)  : Model * Cmd<_>=
    match message, model.Page with
    | SetPage(Home _  ), _ -> initHome remote
    | SetPage(MyOrders _),_ -> initMyOrders remote

    | MyOrdersMsg msg, MyOrders myOrdersModel ->
        let myOrdersModel, cmd = MyOrders.update  msg myOrdersModel.Model
        {model with Page = MyOrders({ Model = myOrdersModel})}, Cmd.map HomeMsg cmd
  
    | HomeMsg (Home.Message.OrderMsg (OrderAccepted _)),_  -> 
        let orderModel = MyOrders.init remote () |> fst
        let init = { Model = orderModel } 
        model, init |> MyOrders |> SetPage |> Cmd.ofMsg

    | HomeMsg msg, Home homeModel ->
        let homeModel, cmd = Home.update remote msg homeModel.Model
        {model with Page = Home({ Model = homeModel})}, Cmd.map HomeMsg cmd
 
    | _ -> failwith "not supported"

        
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
        Program.mkProgram (fun _ -> init remote () ) update view
        |> Program.withRouter (router remote)
#if DEBUG

        |> Program.withConsoleTrace
        |> Program.withErrorHandler (printf "%A")
        |> Program.withHotReload
#endif

