module TagHelperComponents

open Microsoft.AspNetCore.Razor.TagHelpers

open System
open System.Threading.Tasks

type BlazorScriptTagHelperComponent () =
    inherit  TagHelperComponent()
    [<Literal>]
#if WASM
    let script = """<script src="_framework/blazor.webassembly.js"></script>"""
#else
    let script = """<script type="text/javascript" src="blazor.polyfill.min.js"></script>
       <script src="_framework/blazor.server.js"></script>"""
#endif

    override _.ProcessAsync( context : TagHelperContext, output :  TagHelperOutput ) =
        if String.Equals(context.TagName, "body", StringComparison.OrdinalIgnoreCase) then
            output.PostContent.AppendHtml(script) |> ignore
        Task.CompletedTask
