module Services
open Bolero.Remoting.Server
open FBlazorShop.Web
open Microsoft.AspNetCore.Hosting
open FBlazorShop.App
open FBlazorShop.App.Model

type public PizzaService(ctx: IRemoteContext) =
       inherit RemoteHandler<BlazorClient.Services.PizzaService>()

        member private _.GetService<'T>() : 'T = 
            downcast ctx.HttpContext.RequestServices.GetService(typeof<'T>)

        override this.Handler =
           {
               getSpecials = fun () -> 
                let repo = this.GetService<IReadOnlyRepo<PizzaSpecial>>()
                async { 
                    let! b =  
                        repo.Queryable 
                        |> repo.ToListAsync 
                        |> Async.AwaitTask 
                    return b |> List.ofSeq
                }
           }
   