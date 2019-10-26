namespace  FBlazorShop.App
open System.Linq
open System.Collections.Immutable
open System.Threading.Tasks

type IReadOnlyRepo<'T> = 
    abstract member Queryable : IQueryable<'T>
    abstract member ToListAsync : query : IQueryable<'T> -> Task<ImmutableList<'T>>