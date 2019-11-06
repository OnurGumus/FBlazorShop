module FBlazorShop.Web.BlazorClient.Main

open Microsoft.AspNetCore.Components.Routing
open Microsoft.JSInterop
open Bolero.Remoting
open Elmish
open Services
open Orders
open Bolero

type Page =
    | [<EndPoint "/">] Home of Model : PageModel<Home.Model>                  
    | [<EndPoint "/myOrders/{id}">] OrderDetail of id : int * model : PageModel<OrderDetail.Model>
    | [<EndPoint "/myOrders">] MyOrders of Model : PageModel<MyOrders.Model>

type Model = { 
    Page : Page
}

type Message = 
    | SetPage of Page
    | HomeMsg of Home.Message
    | MyOrdersMsg of MyOrders.Message
    | OrderDetailMsg of OrderDetail.Message

let defaultPageModel remote = function
| MyOrders m -> Router.definePageModel m (MyOrders.init remote|> fst)
| Home m ->Router.definePageModel m (Home.init remote |> fst)
| OrderDetail (key, m) -> Router.definePageModel m (OrderDetail.init remote key |> fst)

let router remote = Router.inferWithModel SetPage (fun m -> m.Page) (defaultPageModel remote)

let initPage init msg page =
    let model, cmd = init 
    let page = { Model = model } |> page
    {Page = page }, Cmd.map msg cmd

let initOrderDetail remote key = 
    initPage (OrderDetail.init remote key) OrderDetailMsg (fun pageModel -> OrderDetail(key, pageModel))

let initMyOrders remote = 
    initPage (MyOrders.init remote) MyOrdersMsg MyOrders

let initHome remote = 
    initPage (Home.init remote) HomeMsg Home

let init remote = initHome remote
 
let update remote message (model : Model)  : Model * Cmd<_>=
    
    let genericUpdate update subModel msg  msgFn pageFn =
        let subModel, cmd = update  msg subModel
        {model with Page = pageFn({ Model = subModel})}, Cmd.map msgFn cmd

    match message, model.Page with
    | SetPage(Home _), _ -> initHome remote
    | SetPage(MyOrders _), _ -> initMyOrders remote
    | SetPage(OrderDetail (key, _)), _ -> initOrderDetail remote key 

    | MyOrdersMsg msg, MyOrders myOrdersModel ->
        genericUpdate MyOrders.update (myOrdersModel.Model) msg MyOrdersMsg MyOrders
   
    | HomeMsg (Home.Message.OrderMsg (OrderAccepted _)),_  -> 
        let orderModel = MyOrders.init remote |> fst
        let init = { Model = orderModel } 
        model, init |> MyOrders |> SetPage |> Cmd.ofMsg

    | HomeMsg msg, Home homeModel ->
        genericUpdate (Home.update remote)(homeModel.Model) msg HomeMsg Home

    | OrderDetailMsg(OrderDetail.Message.OrderLoaded _), page 
        when (page |> function | OrderDetail _ -> false | _ -> true) -> 
        model, Cmd.none

    | OrderDetailMsg msg, OrderDetail(key, orderModel) ->
        genericUpdate 
            (OrderDetail.update remote)
            (orderModel.Model)
            msg 
            OrderDetailMsg 
            (fun pageModel -> OrderDetail(key, pageModel))
        
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
        | OrderDetail(_ ,model) -> OrderDetail.view model.Model (OrderDetailMsg >> dispatch)
    
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
        Program.mkProgram (fun _ -> init remote) update view
        |> Program.withRouter (router remote)
#if DEBUG
        |> Program.withConsoleTrace
        |> Program.withErrorHandler (printf "%A")
        |> Program.withHotReload
#endif

