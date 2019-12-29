namespace FBlazorShop.Web

open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection;
open FBlazorShop.EF
open Serilog
open Serilog.Sinks.File
open Serilog.Sinks.SystemConsole
open Microsoft.ApplicationInsights.Extensibility

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
        Log.Logger <-
          LoggerConfiguration().MinimumLevel.Debug()
              .WriteTo.ApplicationInsights(TelemetryConfiguration.CreateDefault(), TelemetryConverter.Traces)

             .WriteTo.File("log.txt", rollingInterval = RollingInterval.Day)
              .WriteTo.Console()
              .CreateLogger();


        let host = CreateHostBuilder(args).Build()
        let scopeFactory = host.Services.GetRequiredService<IServiceScopeFactory>();
        use scope = scopeFactory.CreateScope()
        let db = scope.ServiceProvider.GetRequiredService<PizzaStoreContext>()
        if db.Database.EnsureCreated() then
            Seed.initialize db
        FBlazorShop.Main.init()
        host.Run()

        exitCode
