module BoleroHelpers
open Bolero
open Bolero.Html

let ecompWithAttr<'T, 'model, 'msg when 'T :> ElmishComponent<'model, 'msg>>
    (model: 'model) (dispatch: Elmish.Dispatch<'msg>) attrs =
        comp<'T> ["Model" => model; "Dispatch" => dispatch; yield! attrs] []

let assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name

let prependContent url =
    sprintf "_content/%s/%s" assemblyName url

let errorAndClass name (result:Result<_,Map<string, string list>> option) = 
      match result with
      | Some (Error e) when (e.ContainsKey name && e.[name] <> []) -> System.String.Join(",", e.[name]), "invalid"
      | Some _ -> "", "modified valid"
      | None -> "",""

type FormField = Template<"wwwroot\FormField.html">
let formFieldItem  item fieldType (name : string)  value =
      let error, validClass = errorAndClass name item
      FormField()
          .Label(name)
          .Type(fieldType)
          .ValidClass(validClass)
          .Value(value)
          .Error(error)
          .Elt()