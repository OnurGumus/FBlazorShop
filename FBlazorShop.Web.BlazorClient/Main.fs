module FBlazorShop.Web.BlazorClient.Main

open Microsoft.AspNetCore.Components.Routing
open Microsoft.JSInterop

type Model = { value: int }
let initModel = { value = 0 }

type Message = Increment | Decrement
let update message model =
    match message with
    | Increment -> { model with value = model.value + 1 }
    | Decrement -> { model with value = model.value - 1 }

open Bolero.Html
open Bolero
type MainLayout = Template<"wwwroot\MainLayout.html">

let view model dispatch =
    let content = 
        div [] [
              button [on.click (fun _ -> dispatch Decrement)] [text "-"]
              text (string model.value)
              button [on.click (fun _ -> dispatch Increment)] [text "+"]
          ]
    
    MainLayout()
        .Body(content)
        .Elt()
        
  

open Elmish
open Bolero.Templating.Client

let program =
    Program.mkSimple (fun _ -> initModel) update view
    #if DEBUG
    |> Program.withHotReload
    #endif

type MyApp() =
    inherit ProgramComponent<Model, Message>()

    override this.Program = program
    