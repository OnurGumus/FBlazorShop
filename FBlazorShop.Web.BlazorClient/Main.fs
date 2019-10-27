module FBlazorShop.Web.BlazorClient.Main

open Microsoft.AspNetCore.Components.Routing
open Microsoft.JSInterop
open FBlazorShop.App.Model
open Bolero.Remoting
open Elmish
open Services

type Model = { specials: PizzaSpecial list }
type Message = DataReceived of PizzaSpecial list

let initModel (remote : PizzaService) =
    { specials = ( [] : PizzaSpecial list) }  ,
    Cmd.ofAsync  remote.getSpecials ()  (fun  e -> DataReceived e) ( fun e -> failwith "" )

let update remote message model =
    match message with
    | DataReceived d -> { model with specials = d}, Cmd.none

open Bolero.Html
open Bolero
type MainLayout = Template<"wwwroot\MainLayout.html">
type PizzaCards = Template<"wwwroot\PizzaCards.html">

type ViewItem() =
    inherit ElmishComponent<PizzaSpecial, Message>()

    override __.View special dispatch =
     
        PizzaCards.Item()
            .description(special.Description)
            .imageurl(special.ImageUrl)
            .name(special.Name)
            .price(special.FormattedBasePrice)
            .Elt()

let view ( model : Model) dispatch =
    let content =
        cond  model.specials <| function
        | [] -> empty
        | _ ->
            PizzaCards()
                .Items(forEach model.specials <| fun i ->
                //text (i.ToString()))
                    ecomp<ViewItem,_,_> i dispatch)
                .Elt()
    MainLayout()
        .Body(content)
        .Elt()



open Bolero.Templating.Client
open Services


type MyApp() =
    inherit ProgramComponent<Model, Message>()

    override this.Program =
        let remote = this.Remote<PizzaService>()
        let update = update remote
        let init = initModel remote
        Program.mkProgram (fun _ -> init) update view
        |> Program.withHotReload
