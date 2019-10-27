module Services
open Bolero.Remoting.Server
open FBlazorShop.Web
open Microsoft.AspNetCore.Hosting

type public PizzaService(ctx: IRemoteContext, env: IWebHostEnvironment) =
       inherit RemoteHandler<BlazorClient.Services.PizzaService>()

       override this.Handler =
           {
               getSpecials =  fun () -> async { return Seed.specials }
           }
   