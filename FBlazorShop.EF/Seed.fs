module Seed

open FBlazorShop.EF
open FBlazorShop.App.Model
let specials = [
       {
           Id = 0
           Name = "Basic Cheese Pizza"
           Description = "It's cheesy and delicious. Why wouldn't you want one?"
           BasePrice = 9.99m
           ImageUrl = "img/pizzas/cheese.jpg"
       }
   ]
let initialize (db : PizzaStoreContext) =
   
    db.Specials.AddRange specials
    db.SaveChanges() |> ignore

