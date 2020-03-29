// $begin{copyright}
//
// This file is part of Bolero
//
// Copyright (c) 2018 IntelliFactory and contributors
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License.
//
// $end{copyright}

module Bolero.F

open System
open System.Collections.Generic
open System.Threading.Tasks
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Routing
open Microsoft.JSInterop
open Elmish
open Bolero.Render
open TemplatingInternals
open Microsoft.Extensions.DependencyInjection
open Bolero.Templating
open Bolero.Templating.Client

/// A component built from `Html.Node`s.

type Program<'model, 'msg> = Program<ProgramComponent<'model, 'msg>, 'model, 'msg, Node>

/// A component that runs an Elmish program.
and [<AbstractClass>]
    ProgramComponent<'model, 'msg>() =
    inherit Component<'model>()

    let mutable oldModel = Unchecked.defaultof<'model>
    let mutable navigationInterceptionEnabled = false
    let mutable dispatch = ignore<'msg>
    let mutable rendered = false

    [<Inject>]
    member val NavigationManager = Unchecked.defaultof<NavigationManager> with get, set
    [<Inject>]
    member val Services = Unchecked.defaultof<System.IServiceProvider> with get, set
    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<IJSRuntime> with get, set
    [<Inject>]
    member val NavigationInterception = Unchecked.defaultof<INavigationInterception> with get, set

    member val private View = Empty with get, set
    member _.Dispatch = dispatch
    member val private Router = None : option<IRouter<'model, 'msg>> with get, set

    /// The Elmish program to run. Either this or AsyncProgram must be overridden.
    abstract Program : Program<'model, 'msg>
    default _.Program = Unchecked.defaultof<_>


    interface IProgramComponent with
        member this.Services = this.Services

    member private this.OnLocationChanged (_: obj) (e: LocationChangedEventArgs) =
        this.Router |> Option.iter (fun router ->
            let uri = this.NavigationManager.ToBaseRelativePath(e.Location)
            let route = router.SetRoute uri
            Option.iter dispatch route)

    member internal this.GetCurrentUri() =
        let uri = this.NavigationManager.Uri
        this.NavigationManager.ToBaseRelativePath(uri)

    member internal this.SetState(program, model, dispatch) =
        if this.ShouldRender(oldModel, model) then
            this.ForceSetState(program, model, dispatch)

    member internal _.StateHasChanged() =
        base.StateHasChanged()

    member private this.ForceSetState(program, model, dispatch) =
        this.View <- program.view model dispatch
        oldModel <- model
        if rendered then
            this.InvokeAsync(this.StateHasChanged) |> ignore
        this.Router |> Option.iter (fun router ->
            let newUri = router.GetRoute model
            let oldUri = this.GetCurrentUri()
            if newUri <> oldUri then
                try this.NavigationManager.NavigateTo(newUri)
                with _ -> () // fails if run in prerender
        )

    member this.Rerender() =
        this.ForceSetState(this.Program, oldModel, dispatch)

    member internal _._OnInitialized() =
        base.OnInitialized()

    override this.OnInitialized() =
            this._OnInitialized()
            let program = this.Program
            let setDispatch d =
                dispatch <- d
            { program with
                setState = fun model dispatch ->
                    this.SetState(program, model, dispatch)
                init = fun arg ->
                    let model, cmd = program.init arg
                    model, setDispatch :: cmd
            }
            |> Program.runWith this

    member internal this.InitRouter
        (
            r: IRouter<'model, 'msg>,
            program: Program<'model, 'msg>,
            initModel: 'model
        ) =
        this.Router <- Some r
        EventHandler<_> this.OnLocationChanged
        |> this.NavigationManager.LocationChanged.AddHandler
        match r.SetRoute (this.GetCurrentUri()) with
        | Some msg ->
            program.update msg initModel
        | None ->
            initModel, []

    override this.OnAfterRenderAsync(firstTime) =
        if firstTime then rendered <- true

        if this.Router.IsSome && not navigationInterceptionEnabled then
            navigationInterceptionEnabled <- true
            this.NavigationInterception.EnableNavigationInterceptionAsync()
        else
            Task.CompletedTask


    override this.Render() =
        this.View

    interface System.IDisposable with
        member this.Dispose() =
            EventHandler<_> this.OnLocationChanged
            |> this.NavigationManager.LocationChanged.RemoveHandler

// Attach `router` to `program` when it is run as the `Program` of a `ProgramComponent`.
let withRouter
        (router: IRouter<'model, 'msg>)
        (program: Program<'model, 'msg>) =
    { program with
        init = fun comp ->
            let model, initCmd = program.init comp
            let model, compCmd = comp.InitRouter(router, program, model)
            model, initCmd @ compCmd }

/// Attach a router inferred from `makeMessage` and `getEndPoint` to `program`
/// when it is run as the `Program` of a `ProgramComponent`.
let withRouterInfer
        (makeMessage: 'ep -> 'msg)
        (getEndPoint: 'model -> 'ep)
        (program: Program<'model, 'msg>) =
    program
    |> withRouter (Router.infer makeMessage getEndPoint)


let private registerClient (comp: ProgramComponent<_, _>) =
       match comp.JSRuntime with
       | :? IJSInProcessRuntime as runtime ->
           let settings =
               let s = comp.Services.GetService<HotReloadSettings>()
               if obj.ReferenceEquals(s, null) then HotReloadSettings.Default else s
           let client = new SignalRClient(settings, runtime, comp.NavigationManager)
           TemplateCache.client <- client
           client :> IClient
       | _ ->
           failwith "To use hot reload on the server side, call AddHotReload() in the ASP.NET Core services"

let withHotReload (program: Elmish.Program<ProgramComponent<'model, 'msg>, 'model, 'msg, Node>) =
       { program with
           init = fun comp ->
               let client =
                   // In server mode, the IClient service is set by services.AddHotReload().
                   // In client mode, it is not set, so we create it here.
                   match comp.Services.GetService<IClient>() with
                   | null -> registerClient comp
                   | client -> client
               client.SetOnChange(comp.Rerender)
               program.init comp }
open Bolero.Html

let ecompWithAttr<'T, 'model, 'msg when 'T :> ElmishComponent<'model, 'msg>>
    (model: 'model) (dispatch: Elmish.Dispatch<'msg>) attrs =
        comp<'T> ["Model" => model; "Dispatch" => dispatch; yield! attrs] []

let assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name

let prependContent url =
    sprintf "_content/%s/%s" assemblyName url

let errorAndClass name onFocus (result:Result<_,Map<_,_>> option) =
      match result, onFocus with
      | _ , Some focus when focus = name -> "",""
      | Some (Error e), _ when (e.ContainsKey name && e.[name] <> []) -> System.String.Join(",", e.[name]), "invalid"
      | Some _, _ -> "", "modified valid"
      | _ -> "",""

type FormField = Template<"wwwroot/FormField.html">
let formFieldItem  item onFocus focusMessage fieldType name value =
      let error, validClass = errorAndClass name onFocus item
      FormField()
          .Label(name)
          .Type(fieldType)
          .ValidClass(validClass)
          .OnFocused(focusMessage name)
          .Value(value)
          .Error(error)
          .Elt()

open Microsoft.AspNetCore.Components
open Microsoft.JSInterop
open System

type KeySubscriber() =
    inherit Component()
    interface IDisposable with
        member this.Dispose () =
            this.JsRunTime.InvokeVoidAsync("generalFunctions.removeOnKeyUp") |> ignore

    [<Inject>]
    member val JsRunTime : IJSRuntime = Unchecked.defaultof<_> with get, set
    override _.Render() = empty
    override this.OnAfterRenderAsync firstTime =
        async{
            if firstTime then
                return!
                    this.JsRunTime.InvokeVoidAsync("generalFunctions.registerForOnKeyUp").AsTask()
                    |> Async.AwaitTask
            else
                return ()
        } |> Async.StartImmediateAsTask :> _