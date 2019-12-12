namespace  FBlazorShop.App

open System.Threading.Tasks
open FBlazorShop.App.Model
open System

type IOrderService =
    abstract member PlaceOrder : order : Order -> Task<Result<string,string>>