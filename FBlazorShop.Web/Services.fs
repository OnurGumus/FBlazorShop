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
open System.Security.Cryptography.X509Certificates
open System.Text

module JWT = 
    [<Literal>]
    let ServerRsaPrivateKey = "<RSAKeyValue><Modulus>uYTPtHCIztKC3MUDxnZ0ktGVSQ0jVbD5rYl4pki4RCD3M22d+TklmvTyPj0SM7a/8o7cI05QhEuBI8hKCfC2CEJhlS3WFeVC0vwsl1aYFqQ3Ykr+kDsAdqjL95ioj3JmiscvqKOM34oQahpAgukJ7Kcr1BT2Ylk8fOgKcN7t1qgURNx0Pj4zJ4w0p1nT2gLG++bYutUVPvamI9wcMQyUesZwGmM9UUpMRzsOPk8vv7TbTm62Zkx+5rFUaVe5DFNUIMg92NvyU0392FFNCwptSflidHDG1ayCwL1ZTkJ0Z9yJXCNSzi/3ulxMhE+bVcpr/EuRKCYxn9qPFZ07Bd77bQ==</Modulus><Exponent>AQAB</Exponent><P>2Mrzzbb8Gh7aoW0YXdtdO7WEZ7+pOvbxdp4Qw8sp8dF5cF5vss3I2FoJ9kssy/DsUsreBUhD0HKrADBus7BHKXp7Q/9hhu1nAJxpng255cUfngVD9k1xQdfWEHCeWrr7XJHcplTkh4ysH4nWK+8S+RoCpiuphkJJqVxPzDaY1+M=</P><Q>2xHwflmaMbNs9dXi3wx10SyG5KQJeRIXlKkhlUYlAU+7598AdmTiUPHfhj4WDRCmcJGHjSWqdiuQuwmRYsBXRhtk7XjGAjcefloSpXSR9G+tpVFuIthBU337g2pK1o8z/29LKiWZvcytgxQLEWwGIyduj2I9BoDw1jgFmVd/IG8=</Q><DP>b9n+ghO37G4g1QqpeLtWVhkoEDNFyANiv5V8BtjKclZmdoBy1ujviBikbSuKGErcUzcR593KB0EyUu2qIBGCFbd447NeiTPxYdJRd9eTIyZaUrhawThhh9wpOOAyA5PXXoJvOm4wXnNI1xjRpGc7/cPavAto8rk+sh/LmAxPPYs=</DP><DQ>b2l2N6v2IWSw+22lje5WVOUiTVGnh61N1MsXS0V7OGmGlOvy3kN8XdJE7Y7RxB89pm480+neAW8ykgzRpblQKVVxRNxxR1sk5PmGFiNsvzW0yCjbrFjzEDU4HqOGIAyAU14UigDJaZ+YdttQrbGUhXheYAmEI7SbxzaCknPPMX0=</DQ><InverseQ>SpRpqI+Z4g3jMbb0iE0oD+FAUaBXGp00DjKVbeYH8WQl2rVGFkspFYeN69u3ZFUL3JJd4rCF6zbuLq6iyDJq/F+Jo4zSzXChepr/dSEH1TszaA6imdqFyj3pjOT/ZXNK4YPCRijRM3fy8GdNybZDQljL1djY8D1YK3CWEtKuogs=</InverseQ><D>ADJKztC1SseTfRmPgnZ+DLXAgbflpK6WS3+/9/UcKAsc5LOmA8bytwvkjpPqYNGkH5g7iKU8yP16rbrSXgy6NJ7VYAVENJIhYWKdxxJzAMfvVkeCc4A/sa1GFThwXUG5KBND7EExrsu3oe67LyhOBJXv7vHCvQhSwkZNbiDEtOh7y6bKOOb0aluzPir3eY3HyN7TP2uS5mEeokMvwk9yGOUvCeKoz8t9WJf8HoP2OsDqFsbs5qA66qC6DWaU9OZ0VrO3zgmceIDP2ZXFkWmz2cVJ/Yvfi5zCvc0+g670twnuG8P00Syr/3xNCVuhwwuZbDcILjNvc9uOu9iDbY5xZQ==</D></RSAKeyValue>"
    
    [<Literal>]
    let ServerRsaPublicKey2 = "MIIDfDCCAmSgAwIBAgIQQDCxkdjCQqmQsnSLtcHj3TANBgkqhkiG9w0BAQsFADA7MQswCQYDVQQGEwJ1czELMAkGA1UECBMCVVMxETAPBgNVBAoTCENlcnR0ZXN0MQwwCgYDVQQDEwNqd3QwHhcNMjAwMzIzMDI1NDAzWhcNMjMwMzIzMDMwNDAzWjA7MQswCQYDVQQGEwJ1czELMAkGA1UECBMCVVMxETAPBgNVBAoTCENlcnR0ZXN0MQwwCgYDVQQDEwNqd3QwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQC5hM+0cIjO0oLcxQPGdnSS0ZVJDSNVsPmtiXimSLhEIPczbZ35OSWa9PI+PRIztr/yjtwjTlCES4EjyEoJ8LYIQmGVLdYV5ULS/CyXVpgWpDdiSv6QOwB2qMv3mKiPcmaKxy+oo4zfihBqGkCC6QnspyvUFPZiWTx86Apw3u3WqBRE3HQ+PjMnjDSnWdPaAsb75ti61RU+9qYj3BwxDJR6xnAaYz1RSkxHOw4+Ty+/tNtObrZmTH7msVRpV7kMU1QgyD3Y2/JTTf3YUU0LCm1J+WJ0cMbVrILAvVlOQnRn3IlcI1LOL/e6XEyET5tVymv8S5EoJjGf2o8VnTsF3vttAgMBAAGjfDB6MA4GA1UdDwEB/wQEAwIFoDAJBgNVHRMEAjAAMB0GA1UdJQQWMBQGCCsGAQUFBwMBBggrBgEFBQcDAjAfBgNVHSMEGDAWgBTTMvXgytSFWwQk58CpxCpZAr5G1jAdBgNVHQ4EFgQU0zL14MrUhVsEJOfAqcQqWQK+RtYwDQYJKoZIhvcNAQELBQADggEBAK5vSwzh0x0pJm6njJX29rsd53ktyph+L90Enh0xzLFN0Ku9p+tM8E9TmKR+9ppdPqIEe4G/AuR1fHvmWenEw44M85Y/pBIPZDM2QVQngjg6iRQ42yD5hb/P4+UnvP9a5uI4Xc3f4NlJi3n54qBmdD5Hg52tNYgr8FKRoNzAoUCHelLk5PW0llF8Nc6cjJf0JfrSA1lVua488Dd34sPt798xM3IoISof1dqKslTypHP4BCyZ55SSfQJ+GrY7T9J3ct23BTrPnhhq0sPDogN4j258RmDriBGZmRcnrlmuBD5v+lvjYk0fISYNMfkrCQg5zae4d6BJIZVLY3gITGbaNoA="
    
    let rsa = RSA.Create();
    rsa.FromXmlString(ServerRsaPrivateKey);
    let certPub = new X509Certificate2(Encoding.ASCII.GetBytes(ServerRsaPublicKey2));
    let certPubPriv = new X509Certificate2(certPub.CopyWithPrivateKey(rsa).Export(X509ContentType.Pfx));

type public PizzaService(ctx: IRemoteContext) =
        inherit RemoteHandler<BlazorClient.Services.PizzaService>()
        let secret = "GQDstcKsx0NHjPOuXOYg5MbeJ1XT0uFiwDVvVBrk";

        [<Literal>]
        let EMAIL = "email"

        let generateToken email =
           
            let builder = JwtBuilder();
            let algorithm = RS256Algorithm(JWT.certPubPriv);
            builder
                .WithAlgorithm(algorithm)
                .AddHeader(HeaderName.KeyId, JWT.certPub.Thumbprint)
                .AddClaim("exp", System.DateTimeOffset.UtcNow.AddDays(7.0).ToUnixTimeSeconds())
                .AddClaim(EMAIL, email)
                .Encode()

        let extractUser token =
            let json =
                JwtBuilder()
                    .WithAlgorithm(RS256Algorithm(JWT.certPubPriv))
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
                            <@ fun (t : OrderEntity)->  t.Id = i && t.UserId = user && t.Version >= int64 v @>
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
