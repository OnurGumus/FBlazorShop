module BoleroHelpers
open Bolero
open Bolero.Html

let ecompWithAttr<'T, 'model, 'msg when 'T :> ElmishComponent<'model, 'msg>>
    (model: 'model) (dispatch: Elmish.Dispatch<'msg>) attrs =
        comp<'T> ["Model" => model; "Dispatch" => dispatch; yield! attrs] []