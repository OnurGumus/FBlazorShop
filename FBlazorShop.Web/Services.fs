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
            getOrders = this.GetItems<Order>()

            getOrderWithStatuses = 
                fun () -> 
                    async{
                        let! orders = this.GetItems<Order>()() 
                        let statuses = orders |> List.map OrderWithStatus.FromOrder
                        return statuses
                    }
            getOrderWithStatus = 
                           fun i -> 
                               async{
                                    let! orders = this.GetItems<Order>()() 
                                    let status = 
                                        orders 
                                        |> List.tryFind(fun t -> t.OrderId = i) 
                                        |> Option.map OrderWithStatus.FromOrder
                                    return status
                               }
            placeOrder = 
                fun order -> 
                    async {
                        let orderService = this.GetService<IOrderService>()
                        let! i = order |> orderService.PlaceOrder  |> Async.AwaitTask
                        return i
                    }

        }
   