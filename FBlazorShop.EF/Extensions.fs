namespace FBlazorShop.EF

open System.Runtime.CompilerServices
open Microsoft.Extensions.DependencyInjection
open Microsoft.EntityFrameworkCore
open System
open FBlazorShop.App
open FBlazorShop.App.Model

[<Extension>]
type EFExtensions() =
    [<Extension>]
    static member inline AddEF(services: IServiceCollection, connString : string) = 
        fun (options : DbContextOptionsBuilder) -> 
            connString 
            |> options.UseSqlite  
            |> ignore
        |>  services.AddDbContext<PizzaStoreContext> 
        |> ignore
        services.AddScoped(typedefof<IReadOnlyRepo<_>>, typedefof<ReadOnlyRepo<_>>)