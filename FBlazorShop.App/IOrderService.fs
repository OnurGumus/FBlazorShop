namespace FBlazorShop.App

open System.Threading.Tasks
open FBlazorShop.App.Model
open System

type IOrderService =
    abstract PlaceOrder: order:Order -> Task<Result<(string * int), string>>
