namespace FBlazorShop

open System.Runtime.CompilerServices
open Microsoft.Extensions.DependencyInjection
open System
open FBlazorShop.App
open FBlazorShop.App.Model

[<Extension>]
type EFExtensions() =
    [<Extension>]
    static member inline SetupServices(services: IServiceCollection) = 
        services
