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

        member private this.GetItems<'T>() =
            fun () -> 
                let repo = this.GetService<IReadOnlyRepo<'T>>()
                async { 
                      let! b =  
                          repo.Queryable 
                          |> repo.ToListAsync 
                          |> Async.AwaitTask 
                      return b |> List.ofSeq
                }
        override this.Handler = {
        
            getSpecials = this.GetItems<PizzaSpecial>()
            getToppings = this.GetItems<Topping>()
            getOrders  = fun token -> 
                             async{
                                 let! orders = this.GetItems<Order>()() 
                                 let statuses = orders
                                 return statuses
                             }

            getOrderWithStatuses = 
                fun token -> 
                    async{
                        let! orders = this.GetItems<Order>()() 
                        let statuses = orders |> List.map OrderWithStatus.FromOrder
                        return statuses
                    }
            getOrderWithStatus = 
                           fun (token,i) -> 
                               async{
                                    let! orders = this.GetItems<Order>()() 
                                    let status = 
                                        orders 
                                        |> List.tryFind(fun t -> t.OrderId = i) 
                                        |> Option.map OrderWithStatus.FromOrder
                                    return status
                               }
            placeOrder = 
                fun (token,order) -> 
                    async {
                        let orderService = this.GetService<IOrderService>()
                        let! i = order |> orderService.PlaceOrder  |> Async.AwaitTask
                        return i
                    }
            signIn = 
                fun (email, pass) -> 
                    async { 
                        match pass with 
                        | "Password" ->
                            return Ok( { User = email ; Token = email ; TimeStamp = System.DateTime.Now} )
                        | _ -> return Error("Invalid login. Try Password as password")
                    }
        }
   