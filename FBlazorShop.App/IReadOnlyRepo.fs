namespace  FBlazorShop.App
open System.Linq
open System.Threading.Tasks
open System.Collections.Generic

type IReadOnlyRepo<'T> =
    abstract member Queryable : IQueryable<'T>
    abstract member ToListAsync : query : IQueryable<'T> -> Task<IReadOnlyList<'T>>