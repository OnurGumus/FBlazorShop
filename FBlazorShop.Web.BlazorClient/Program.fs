module Program

open Microsoft.AspNetCore.Blazor.Hosting
open FBlazorShop.Web.BlazorClient

[<EntryPoint>]
let Main args =
    BlazorWebAssemblyHost.CreateDefaultBuilder()
        .UseBlazorStartup<Main.Startup>()
        .Build()
        .Run()
    0