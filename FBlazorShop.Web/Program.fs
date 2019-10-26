namespace FBlazorShop.Web

open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Threading.Tasks
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection;
open Microsoft.Extensions.Hosting;
open FBlazorShop.EF

module Program =
    let exitCode = 0

    let CreateHostBuilder args =
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(fun webBuilder ->
                webBuilder.UseStartup<Startup>() |> ignore
            )

    [<EntryPoint>]
    let main args =
        let host = CreateHostBuilder(args).Build()
        let scopeFactory = host.Services.GetRequiredService<IServiceScopeFactory>();
        use scope = scopeFactory.CreateScope()
        let db = scope.ServiceProvider.GetRequiredService<PizzaStoreContext>()
        if db.Database.EnsureCreated() then
            Seed.initialize db
               
        host.Run()

        exitCode
