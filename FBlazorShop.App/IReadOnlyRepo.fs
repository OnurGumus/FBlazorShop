namespace FBlazorShop.App

open System.Linq
open System.Threading.Tasks
open System.Collections.Generic

type IReadOnlyRepo<'T> =
    abstract Queryable: IQueryable<'T>
    abstract ToListAsync: query:IQueryable<'T> -> Task<IReadOnlyList<'T>>
