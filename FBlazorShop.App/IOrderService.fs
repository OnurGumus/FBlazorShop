namespace  FBlazorShop.App

open System.Threading.Tasks
open FBlazorShop.App.Model

type IOrderService = 
    abstract member PlaceOrder : order : Order -> Task<int>