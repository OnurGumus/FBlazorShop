namespace FBlazorShop.ComponentsLibrary
open Microsoft.JSInterop
open Bolero
open Microsoft.AspNetCore.Components

//@using Microsoft.JSInterop
//@inject IJSRuntime JSRuntime

//<div id="@elementId" style="height: 100%; width: 100%;"></div>

//@code {
//    string elementId = $"map-{Guid.NewGuid().ToString("D")}";
    
//    [Parameter] public double Zoom { get; set; }
//    [Parameter] public List<Marker> Markers { get; set; }

//    protected async override Task OnAfterRenderAsync(bool firstRender)
//    {
//        await JSRuntime.InvokeVoidAsync(
//            "deliveryMap.showOrUpdate",
//            elementId,
//            Markers);
//    }
//}
open Bolero.Html
open System.Linq

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
        this.JSRuntime.InvokeVoidAsync("deliveryMap.showOrUpdate", elementId, (this.Markers.ToList())).AsTask()
        