namespace FBlazorShop.Web

open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection;
open FBlazorShop.EF

module Program =
    let exitCode = 0

    let CreateHostBuilder args =
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(fun webBuilder ->
                webBuilder.UseStartup<Startup>() |> ignore
                webBuilder.UseSetting(WebHostDefaults.DetailedErrorsKey, "true") |> ignore
            )

    [<EntryPoint>]
    let main args =
        let host = CreateHostBuilder(args).Build()
        let scopeFactory = host.Services.GetRequiredService<IServiceScopeFactory>();
        use scope = scopeFactory.CreateScope()
        let db = scope.ServiceProvider.GetRequiredService<PizzaStoreContext>()
        if db.Database.EnsureCreated() then
            Seed.initialize db
        Projection.init() |> ignore
        host.Run()

        exitCode
