namespace FBlazorShop.ComponentsLibrary
open Microsoft.JSInterop
open Bolero
open Microsoft.AspNetCore.Components
open Bolero.Html

type Map() =
    inherit Component()
    let elementId = "map-"+ System.Guid.NewGuid().ToString("D");

    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<IJSRuntime> with get, set

    [<Parameter>]
    member val Zoom = 0.0 with get, set

    [<Parameter>]
    member val Markers = [] : FBlazorShop.App.Model.Marker list with get, set
    override __.Render() =
        div [attr.id elementId; attr.style "height: 100%; width: 100%;"][]

    override this.OnAfterRenderAsync _ =
        this.JSRuntime.InvokeVoidAsync("deliveryMap.showOrUpdate", elementId, (this.Markers)).AsTask()
