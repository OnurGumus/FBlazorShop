module FBlazorShop.Web.BlazorClient.Main

open Microsoft.AspNetCore.Components.Routing
open Microsoft.JSInterop
open Bolero.Remoting
open Elmish
open Services
open Orders
open Bolero
open Newtonsoft.Json
open FBlazorShop.App.Model
open System

type Page =
    | Start
    | [<EndPoint "/">] Home of Model : PageModel<Home.Model>
    | [<EndPoint "/myOrders/{id}">] OrderDetail of id : string * model : PageModel<OrderDetail.Model>
    | [<EndPoint "/myOrders">] MyOrders of Model : PageModel<MyOrders.Model>
    | [<EndPoint "/checkout">] Checkout of Model : PageModel<Checkout.Model>

type Model = {
    Page : Page
    State : Common.State
    BufferedCommand : Cmd<Message>
    SignIn : SignIn.Model
    Specials : PizzaSpecial list
}

and Message=

    | SetPage of Page
    | HomeMsg of Home.Message
    | MyOrdersMsg of MyOrders.Message
    | OrderDetailMsg of OrderDetail.Message
    | CheckoutMsg of Checkout.Message
    | Rendered
    | TokenRead of Common.Authentication
    | TokenSet
    | TokenNotFound
    | TokenSaved of Common.Authentication
    | SignOutRequested
    | SignedOut
    | CommonMessage of Common.Message
    | SignInMessage of SignIn.Message
    | RemoveBuffer
    | TokenRenewed of Common.Authentication
    | OrderCleared

let defaultPageModel remote jsRuntime = function
| MyOrders m -> Router.definePageModel m (MyOrders.init remote|> fst)
| Home m ->Router.definePageModel m (Home.init remote jsRuntime|> fst)
| OrderDetail (key, m) -> Router.definePageModel m (OrderDetail.init  ((if isNull key then "" else key ) , None)|> fst)
| Checkout m -> Router.definePageModel m (Checkout.init remote None|> fst)
| Start -> ()
let router remote jsRuntime = Router.inferWithModel SetPage (fun m -> m.Page) (defaultPageModel remote jsRuntime)

let initPage  init (model : Model) msg page =
    let pageModel, cmd = init
    let page = { Model = pageModel } |> page
    { model with Page  = page; }, Cmd.map msg cmd

let initOrderDetail remote key model =
    initPage  (OrderDetail.init  (key ,None)) model OrderDetailMsg
        (fun pageModel -> OrderDetail(key.ToString(), pageModel))

let initMyOrders remote  model =
    initPage  (MyOrders.init remote) model MyOrdersMsg MyOrders

let initCheckout remote model order =
    initPage   (Checkout.init remote order) model CheckoutMsg Checkout

let initHome remote jsrunTime model =
    initPage (Home.init remote jsrunTime) model HomeMsg Home


let init specials =
    {   Page = Start;
        State  = { Authentication = Common.AuthState.NotTried};
        SignIn = SignIn.init() |> fst
        BufferedCommand = Cmd.none
        Specials = specials
     }, Cmd.none


let renewTokenCmd (remote : PizzaService) token =
    let doWork token =
        async{
            let! newToken = remote.renewToken token
            return
                match newToken with
                | Ok t -> t
                | _ -> failwith "auth error"
        }
    Cmd.ofAsync doWork token TokenRenewed (Common.Error >> CommonMessage)

let clearOrder (jsRuntime : IJSRuntime) =
    let doWork () =
        async{
            do!
                jsRuntime.InvokeVoidAsync("window.localStorage.removeItem", "pizzas")
                    .AsTask()
                    |> Async.AwaitTask
            return OrderCleared
        }
    Cmd.ofAsync doWork () id raise

let getToken (jsRuntime : IJSRuntime)  =
    let doWork () =
        async{
            let! res =
                jsRuntime.InvokeAsync<string>("window.localStorage.getItem", "token")
                    .AsTask()
                    |> Async.AwaitTask
            return
                match res with
                | null -> TokenNotFound
                | t -> t |> JsonConvert.DeserializeObject<Common.Authentication> |> TokenRead
        }
    Cmd.ofAsync doWork () id (fun _ -> TokenNotFound)

let signOut (jsRuntime : IJSRuntime)  =
    let doWork () =
        async{
            do!
                jsRuntime.InvokeVoidAsync("window.localStorage.removeItem", "token")
                    .AsTask()
                    |> Async.AwaitTask
            return SignedOut
        }
    Cmd.ofAsync doWork () id raise

let signInCmd (jsRuntime : IJSRuntime) (token : Common.Authentication)  =
    let doWork () =
        async{
            let ser = JsonConvert.SerializeObject(token)
            do!
                jsRuntime.InvokeVoidAsync("window.localStorage.setItem", "token" , ser)
                    .AsTask()
                    |> Async.AwaitTask
            return TokenSaved token
        }
    Cmd.ofAsync doWork () id raise

let update remote jsRuntime message model =
    let genericUpdate update subModel msg  msgFn pageFn =
        let subModel, cmd = update  msg subModel
        {model with Page = pageFn({ Model = subModel})}, Cmd.map msgFn cmd

    let genericUpdateWithCommon update subModel msg  msgFn pageFn =
        let subModel, cmd, (commonCommand  : Cmd<Common.Message>) = update  msg (subModel, model.State)
        if commonCommand |> List.isEmpty then
            {model with Page = pageFn({ Model = subModel})},  Cmd.map msgFn cmd
        else
            let m = {model with Page = pageFn({ Model = subModel}); BufferedCommand = Cmd.map msgFn cmd}
            m,Cmd.map CommonMessage commonCommand


    match message, model.Page with
    | RemoveBuffer, _ -> { model with BufferedCommand = Cmd.none}, Cmd.none
    | Rendered, _ -> model, getToken jsRuntime
    | SetPage(Checkout _), Start
    | SetPage(Start), _
    | SetPage(Home _), _ -> initHome remote jsRuntime model
    | SetPage(MyOrders _), _
        when( model.State.Authentication |> function  Common.AuthState.Failed -> true | _ -> false )->
            {model with BufferedCommand = Cmd.ofMsg(message)}, Cmd.ofMsg(CommonMessage Common.AuthenticationRequested)
    | SetPage(MyOrders _), _ -> initMyOrders remote model
    | SetPage(OrderDetail (key, _)), _ -> initOrderDetail remote (key) model
    | SetPage(Checkout _), Checkout _ -> model, Cmd.none
    | SetPage(Checkout m), _ -> initCheckout remote model m.Model.Order
    | TokenRead t , _ ->  model, renewTokenCmd remote t.Token
    | TokenRenewed t , _ ->
        { model with  State = { Authentication = Common.AuthState.Success t }; }, Cmd.ofMsg TokenSet
    | SignOutRequested, _ -> model , Cmd.batch[ clearOrder jsRuntime;signOut jsRuntime;]
    | SignedOut, _ ->
        let model, cmd = init model.Specials
        {model with State = { Authentication = Common.AuthState.Failed}}, cmd

    | TokenNotFound , _ ->
        { model with State = {model.State with Authentication = Common.AuthState.Failed} }, Cmd.none
    | TokenSet, MyOrders _ ->
        model, MyOrders.reloadCmd |> Cmd.map MyOrdersMsg
    | TokenSet, OrderDetail _ ->
        model, OrderDetail.reloadCmd |> Cmd.map OrderDetailMsg

    | MyOrdersMsg msg, MyOrders myOrdersModel ->
        genericUpdateWithCommon (MyOrders.update remote) (myOrdersModel.Model) msg MyOrdersMsg MyOrders

    | HomeMsg (Home.Message.OrderMsg (CheckoutRequested o)),_  ->
        let orderModel = Checkout.init remote (Some o) |> fst
        let init = { Model = orderModel }
        model, init |> Checkout |> SetPage |> Cmd.ofMsg

    | HomeMsg msg, Home homeModel ->
        genericUpdate (Home.update remote jsRuntime)(homeModel.Model) msg HomeMsg Home

    | CheckoutMsg (Checkout.Message.OrderAccepted o), _  ->
        let orderModel = OrderDetail.init  (o,None) |> fst
        let init = { Model = orderModel }
        model, Cmd.batch[ (o.ToString(),init) |> OrderDetail |> SetPage |> Cmd.ofMsg ; clearOrder jsRuntime]

    | CheckoutMsg msg, Checkout model ->
        let u = Checkout.update remote
        genericUpdateWithCommon u (model.Model) msg CheckoutMsg Checkout

    | OrderDetailMsg(OrderDetail.Message.OrderLoaded _), page
        when (page |> function | OrderDetail _ -> false | _ -> true) ->
        model, Cmd.none

    | OrderDetailMsg msg, OrderDetail(key, orderModel) ->
        genericUpdateWithCommon
            (OrderDetail.update remote)
            (orderModel.Model)
            msg
            OrderDetailMsg
            (fun pageModel -> OrderDetail(key, pageModel))

    | CommonMessage (Common.Message.AuthenticationRequested), _ ->
        let m , cmd = SignIn.update remote SignIn.SignInRequested model.SignIn
        {model with SignIn = m}, Cmd.map SignInMessage cmd

    | SignInMessage (SignIn.Message.SignInDone c), _ ->  model, signInCmd jsRuntime c

    | SignInMessage msg, _ ->
        let m, cmd = SignIn.update remote msg (model.SignIn)
        {model with SignIn = m }, Cmd.map SignInMessage cmd

    | TokenSaved t, _ ->
     { model with State = { model.State with Authentication = Common.AuthState.Success t}},
        Cmd.batch[ model.BufferedCommand; Cmd.ofMsg(RemoveBuffer)]
    | TokenSet , _ -> model , Cmd.none
    | OrderCleared , _ -> model, Cmd.none
    | msg, model -> invalidOp   (msg.ToString() + " === " + model.ToString())

open Bolero.Html
open BoleroHelpers

type MainLayout = Template<"wwwroot\MainLayout.html">

type LoginDisplay() =
    inherit ElmishComponent<Common.AuthState, Message>()
    override _.View model dispatch =
        cond model <| function
                | Common.AuthState.NotTried | Common.AuthState.Failed->
                    a [attr.``class`` "sign-in";
                        on.click(fun _ ->  CommonMessage (Common.Message.AuthenticationRequested) |> dispatch)]
                        [text "Sign in"]
                | Common.AuthState.Success {User = user} ->
                    concat [
                        img [attr.src ("img/user.svg" |> prependContent)]
                        div [] [
                            span [attr.``class`` "username"] [text user]
                            a [attr.``class`` "sign-out"; on.click (fun _ -> SignOutRequested |> dispatch)][text "Sign out"]
                        ]
                   ]

let view  (js: IJSRuntime) ( model : Model) dispatch =
    let content =
        cond model.Page <| function
        | Home (model) ->
          Home.view model.Model (HomeMsg >> dispatch)
        | MyOrders model -> MyOrders.view model.Model (MyOrdersMsg >> dispatch)
        | OrderDetail(_ ,model) -> OrderDetail.view model.Model (OrderDetailMsg >> dispatch)
        | Checkout m -> Checkout.view m.Model (CheckoutMsg >> dispatch)
        | Start -> h2 [] [text "Loading ..."]

    let loginDisplay = ecomp<LoginDisplay,_,_> model.State.Authentication dispatch

    let signIn = SignIn.view model.SignIn (SignInMessage >> dispatch  )

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
        .LoginDisplay(loginDisplay)
        .SignIn(signIn)
        .Elt()

open System
open Bolero.Templating.Client

open Microsoft.AspNetCore.Components

type MyApp() =
    inherit ProgramComponent<Model, Message>()

    let  dispatch =
        typeof<MyApp>
            .GetProperty("Dispatch", Reflection.BindingFlags.Instance ||| Reflection.BindingFlags.NonPublic)

    static let mutable dispatcher: (Message -> unit) = Unchecked.defaultof<_>

    [<Parameter>]
    member val Specials : PizzaSpecial list = [] with get, set

    [<JSInvokable>]
    static member  ReturnArrayAsync(message: obj) =
        Console.WriteLine(message)
        System.Threading.Tasks.Task.FromResult([| 1; 2; 3 |]);


    override this.OnAfterRenderAsync(firstRender) =
        let res = base.OnAfterRenderAsync(firstRender) |> Async.AwaitTask
        async{
            do! res
            if firstRender then
                dispatcher <- downcast dispatch.GetValue(this)
                dispatcher Rendered
            return ()
         }|> Async.StartImmediateAsTask :> _

    override this.Program =
        let remote = this.Remote<PizzaService>()
        let update = update remote (this.JSRuntime)
        let router = router remote (this.JSRuntime)
        Program.mkProgram (fun _ -> init this.Specials) (update) (view this.JSRuntime)
        |> Program.withRouter router
    //    |> Program.withSubscription (fun _ -> Rendered |> Cmd.ofMsg)
#if DEBUG
        |> Program.withTrace(fun msg model -> System.Console.WriteLine(msg))
        |> Program.withConsoleTrace
        |> Program.withErrorHandler
            (fun (x,y) ->
                Console.WriteLine("Error Message:" + x)
                Console.WriteLine("Exception:" + y.ToString()))

        |> Program.withHotReload
#endif

open Microsoft.AspNetCore.Components.Builder
open Microsoft.Extensions.DependencyInjection
open Bolero.Remoting.Client
type Startup() =
    member __.ConfigureServices(services: IServiceCollection) =
        services.AddRemoting()

    member __.Configure(app: IComponentsApplicationBuilder) =
        app.AddComponent<MyApp>("app")
