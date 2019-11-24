module FBlazorShop.App.Domain
open System
    
type String50 = private String50 of string

module Validation =
    
    let ifTrueThen succ = function
        | true -> Some succ
        | false -> None
    
    let (|NullOrEmpty|_|) = 
        String.IsNullOrEmpty 
        >> ifTrueThen NullOrEmpty
    
    let (|StringLength|_|) l s = 
        (String.length s) > l 
        |> ifTrueThen StringLength
    
    let string50 property = function
        | NullOrEmpty -> Error <|  Map.add property  "{0} cannot be null or empty"
        | StringLength 50 -> Error <| Map.add property "{0} cannot be longer than 50 chars"
        | s -> String50 s |> Ok 

type ValidAddress ={
    Name: String50
    Line1 : String50 
    Line2 : String50
    City : String50
    Region : String50
    PostalCode : String50
}
open FBlazorShop.App.Model

//type ValidateAddress = Address -> ValidAddress 


let creatValidAddress  (address : Address) =
        let name = Validation.string50 ( nameof address.Name) address.Name 
        {| Name = name|}
