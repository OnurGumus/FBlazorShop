namespace FBlazorShop.Web

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open FBlazorShop.EF
open Bolero.Templating.Server
open Bolero.Remoting.Server
open FBlazorShop
open Blazor
open Blazor.Extensions
type Startup() =
    member _.ConfigureServices(services: IServiceCollection) =
        services
        #if DEBUG
            .AddHotReload(templateDir = "../FBlazorShop.Web.BlazorClient")
        #endif
            .AddRemoting<Services.PizzaService>() 
            .AddEF("Data Source=pizza.db") 
            .SetupServices()
            .AddMvc()
            
            .AddRazorRuntimeCompilation() |> ignore
        #if !WASM           
        services.AddServerSideBlazor()|> ignore
        #endif
        

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member _.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =

        if env.IsDevelopment() then
            app.UseDeveloperExceptionPage() |> ignore
        #if DEBUG
        app.UseBlazorDebugging()
        #endif
        app
            .UseRemoting()
            .UseRouting()
        #if WASM
            .UseClientSideBlazorFiles<FBlazorShop.Web.BlazorClient.Main.Startup>()
        #endif
            .UseStaticFiles() 
            .UseEndpoints(fun endpoints ->
#if !WASM
                endpoints.MapBlazorHub() |> ignore
#endif
                endpoints.MapFallbackToPage("/_Host") |> ignore
#if DEBUG
                endpoints.UseHotReload()
#endif
            ) |> ignore
