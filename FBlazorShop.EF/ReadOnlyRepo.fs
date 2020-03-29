namespace FBlazorShop.EF
open FBlazorShop.App
open System.Linq
open System.Threading.Tasks
open Microsoft.EntityFrameworkCore
open FSharp.Control.Tasks.V2
open System.Collections.Generic
open FBlazorShop.App.Model

type ReadOnlyRepo<'T> ( dataContext : PizzaStoreContext) =
    interface IReadOnlyRepo<'T> with
        member _.Queryable: IQueryable<'T> =
            if typeof<'T> = typeof<PizzaSpecial> then
                dataContext.Specials.AsQueryable() :?> IQueryable<'T>
            elif typeof<'T> = typeof<Topping> then
                dataContext.Toppings.AsQueryable() :?> IQueryable<'T>
            else
                invalidOp "unknown type"

        member _.ToListAsync(query: IQueryable<'T>) : Task<IReadOnlyList<'T>> =
            task{
                let! items = query.ToListAsync()
                return upcast items
            }

