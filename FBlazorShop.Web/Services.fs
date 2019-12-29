module Services
open Bolero.Remoting.Server
open FBlazorShop.Web
open Microsoft.AspNetCore.Hosting
open FBlazorShop.App
open FBlazorShop.App.Model
open JWT.Builder
open JWT
open System.Security.Cryptography
open JWT.Algorithms
open System.Collections.Generic
open FBlazorShop.Web.BlazorClient.Main
open Projection
open System.Linq
open System.Linq.Expressions

type public PizzaService(ctx: IRemoteContext) =
        inherit RemoteHandler<BlazorClient.Services.PizzaService>()
        let secret = "GQDstcKsx0NHjPOuXOYg5MbeJ1XT0uFiwDVvVBrk";
        [<Literal>]
        let EMAIL = "email"

        let generateToken email =
            JwtBuilder()
                .WithAlgorithm(new HMACSHA256Algorithm())
                .WithSecret(secret)
                .AddClaim("exp", System.DateTimeOffset.UtcNow.AddDays(7.0).ToUnixTimeSeconds())
                .AddClaim(EMAIL, email)
                .Build()

        let extractUser token =
            let json =
                JwtBuilder()
                    .WithSecret(secret)
                    .MustVerifySignature()
                    .Decode<IDictionary<string, string>>(token);
            json.[EMAIL]

        member private _.GetService<'T>() : 'T =
            downcast ctx.HttpContext.RequestServices.GetService(typeof<'T>)

        member private this.GetItems<'T>
            (filter :Expression<System.Func<'T,bool>> option) take skip () =
                let repo = this.GetService<IReadOnlyRepo<'T>>()
                async {
                      let! b =
                          let q = repo.Queryable
                          let q =
                              match filter with
                              | Some f -> q.Where f
                              | _ -> q
                          let q =
                              match take with
                              | Some t -> q.Take t
                              | _ -> q
                          let q =
                            match skip with
                            | Some t -> q.Skip t
                            | _ -> q
                          q
                          |> repo.ToListAsync
                          |> Async.AwaitTask
                      return b |> List.ofSeq
                }
        override this.Handler = {

            getSpecials = this.GetItems<PizzaSpecial> None None None
            getToppings = this.GetItems<Topping> None None None
            getOrders  = fun token ->
                             let user = extractUser token
                             async{
                                 let! orders = this.GetItems<OrderEntity> None None None ()
                                 let orders = orders |> List.map Projection.toOrder
                                 return orders |> List.filter (fun t-> t.UserId = user)
                             }

            getOrderWithStatuses =
                fun token ->
                    let user = extractUser token
                    async{
                        let filter  =
                            <@ fun (t : OrderEntity)-> t.UserId = user @>
                            |>  Common.QuotationHelpers.toLinq
                            |> Some
                        let! orders =
                            this.GetItems<OrderEntity> filter None None ()
                        let orders = orders |> List.map Projection.toOrder
                        return
                            orders
                            |> List.filter (fun t-> t.UserId = user)
                            |> List.map OrderWithStatus.FromOrder
                    }
            getOrderWithStatus =
                fun (token, i, v) ->
                    let user = extractUser token
                    async {
                        let filter  =
                            <@ fun (t : OrderEntity)->  t.Id = i && t.UserId = user @>
                            |>  Common.QuotationHelpers.toLinq
                            |> Some
                        let! orders = this.GetItems<OrderEntity> filter (Some 1) None ()
                        return
                            orders
                            |> List.map Projection.toOrder
                            |> List.tryExactlyOne
                            |> Option.map OrderWithStatus.FromOrder
                    }

            renewToken =
                fun token ->
                    let user = extractUser token
                    let token = generateToken user
                    async {  return Ok( { User = user ; Token = token ; TimeStamp = System.DateTime.Now} )}
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
                            let token = generateToken email
                            return Ok( { User = email ; Token = token ; TimeStamp = System.DateTime.Now} )
                        | _ ->
                        //    for (d:Message -> unit) in MyApp.Dispatchers.Keys do
                          //                       d(SignOutRequested)

                            return Error("Invalid login. Try Password as password")
                    }
        }
