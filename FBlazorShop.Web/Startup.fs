namespace FBlazorShop.Web

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.EntityFrameworkCore
open FBlazorShop.EF
open Bolero.Templating.Server
open Bolero.Remoting.Server
open Bolero.Remoting
type Startup() =

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    member this.ConfigureServices(services: IServiceCollection) =
     //   services.AddRazorPages() |> ignore
        services
        #if DEBUG
            .AddHotReload(templateDir = "../FBlazorShop.Web.BlazorClient/wwwroot")
        #endif
        |> ignore
        services.AddRemoting<Services.PizzaService>() |> ignore

        services.AddMvc().AddRazorRuntimeCompilation() |> ignore
        services
            .AddEF("Data Source=pizza.db") 
            .AddServerSideBlazor()
            
            |> ignore

        

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member this.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =
        if env.IsDevelopment() then
            app.UseDeveloperExceptionPage() |> ignore

       
        app
            .UseRemoting()
            .UseRouting()
            .UseStaticFiles() 
            |> ignore

        app.UseEndpoints(fun endpoints ->
            endpoints.MapBlazorHub() |> ignore
            #if DEBUG
            endpoints.UseHotReload()
            #endif
            endpoints.MapFallbackToPage("/_Host") |> ignore
            ) |> ignore
