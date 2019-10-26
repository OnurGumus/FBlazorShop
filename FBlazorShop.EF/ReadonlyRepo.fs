namespace FBlazorShop.EF
open FBlazorShop.App
open System.Linq
open System.Threading.Tasks
open System.Collections.Immutable
open Microsoft.EntityFrameworkCore
open FSharp.Control.Tasks.V2

type ReadOnlyRepo<'T> ( dataContext : PizzaStoreContext) = 
    interface IReadOnlyRepo<'T> with
        member this.Queryable: IQueryable<'T> = 
            dataContext.Specials.AsQueryable() :?> IQueryable<'T>
        member this.ToListAsync(query: IQueryable<'T>): Task<ImmutableList<'T>> = 
            task{
                let! items = query.ToListAsync()
                return ImmutableList.CreateRange items
            }
          
