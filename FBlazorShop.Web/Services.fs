module Services
open Bolero.Remoting.Server
open FBlazorShop.Web
open Microsoft.AspNetCore.Hosting
open FBlazorShop.App
open FBlazorShop.App.Model

type public PizzaService(ctx: IRemoteContext) =
        inherit RemoteHandler<BlazorClient.Services.PizzaService>()

        let extractUser token = token

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
                             let user = extractUser token    
                             async{
                                 let! orders = this.GetItems<Order>()() 
                                 return orders |> List.filter (fun t-> t.UserId = user)
                             }

            getOrderWithStatuses = 
                fun token -> 
                    let user = extractUser token    
                    async{
                        let! orders = this.GetItems<Order>()()
                        return
                            orders 
                            |> List.filter (fun t-> t.UserId = user) 
                            |> List.map OrderWithStatus.FromOrder
                    }
            getOrderWithStatus = 
                            fun (token, i) -> 
                                let user = extractUser token    
                                async{
                                    let! orders = this.GetItems<Order>()() 
                                    let status = 
                                        orders 
                                        |> List.tryFind(fun t -> t.OrderId = i && t.UserId = user) 
                                        |> Option.map OrderWithStatus.FromOrder
                                    return status
                                }
            placeOrder = 
                fun (token, order) -> 
                    let user = extractUser token
                    async {
                        let order = {order with UserId = user }
                        let orderService = this.GetService<IOrderService>()
                        return! order |> orderService.PlaceOrder  |> Async.AwaitTask
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
   