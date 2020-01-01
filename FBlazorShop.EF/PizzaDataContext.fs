namespace FBlazorShop.EF
open Microsoft.EntityFrameworkCore
open FBlazorShop.App.Model

type PizzaStoreContext =
    inherit DbContext

    [<DefaultValue>]
    val mutable private specials : DbSet<PizzaSpecial>

    [<DefaultValue>]
    val mutable private offsets : DbSet<Offset>

    [<DefaultValue>]
    val mutable private toppings : DbSet<Topping>
    new () = {}
    new (options : DbContextOptions) = { inherit DbContext(options) }
    override _.OnModelCreating( modelBuilder) =
           modelBuilder
            .Entity<Offset>()
            .HasKey("OffsetName")
            |> ignore


    member this.Specials
        with get() = this.specials
        and private set v = this.specials <- v
    member this.Offsets
           with get() = this.offsets
           and private set v = this.offsets <- v

    member this.Toppings
        with get() = this.toppings
        and private set v = this.toppings <- v