module Bolero.F

open System
open Microsoft.AspNetCore.Components
open Microsoft.JSInterop
open Bolero.Html

let ecompWithAttr<'T, 'model, 'msg when 'T :> ElmishComponent<'model, 'msg>> (model: 'model)
                                                                             (dispatch: Elmish.Dispatch<'msg>)
                                                                             attrs
                                                                             =
    comp<'T>
        [
            "Model" => model
            "Dispatch" => dispatch
            yield! attrs
        ]
        []

let assemblyName =
    System.Reflection.Assembly.GetExecutingAssembly().GetName().Name

let prependContent url =
    sprintf "_content/%s/%s" assemblyName url

let errorAndClass name onFocus (result: Result<_, Map<_, _>> option) =
    match result, onFocus with
    | _, Some focus when focus = name -> "", ""
    | Some (Error e), _ when (e.ContainsKey name && e.[name] <> []) -> System.String.Join(",", e.[name]), "invalid"
    | Some _, _ -> "", "modified valid"
    | _ -> "", ""

type FormField = Template<"wwwroot/FormField.html">

let formFieldItem item onFocus focusMessage fieldType name value =
    let error, validClass = errorAndClass name onFocus item

    FormField().Label(name).Type(fieldType).ValidClass(validClass).OnFocused(focusMessage name).Value(value)
        .Error(error).Elt()

type KeySubscriber() =
    inherit Component()

    interface IDisposable with
        member this.Dispose() =
            this.JsRunTime.InvokeVoidAsync("generalFunctions.removeOnKeyUp")
            |> ignore

    [<Inject>]
    member val JsRunTime: IJSRuntime = Unchecked.defaultof<_> with get, set

    override _.Render() = empty

    override this.OnAfterRenderAsync firstTime =
        async {
            if firstTime then
                return!
                    this.JsRunTime.InvokeVoidAsync("generalFunctions.registerForOnKeyUp").AsTask()
                    |> Async.AwaitTask
            else
                return ()
        }
        |> Async.StartImmediateAsTask :> _
