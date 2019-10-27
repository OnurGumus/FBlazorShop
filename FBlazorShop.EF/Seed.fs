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
       {
           Id = 2
           Name = "The Baconatorizor"
           Description = "It has EVERY kind of bacon"
           BasePrice = 11.99m
           ImageUrl = "img/pizzas/bacon.jpg"
       }
     
       {
           Id = 3
           Name = "Classic pepperoni"
           Description = "It's the pizza you grew up with, but Blazing hot!"
           BasePrice = 10.50m
           ImageUrl = "img/pizzas/pepperoni.jpg"
       }
       {
           Id = 4
           Name = "Buffalo chicken"
           Description = "Spicy chicken, hot sauce and bleu cheese, guaranteed to warm you up"
           BasePrice = 12.75m
           ImageUrl = "img/pizzas/meaty.jpg"
       }
       {
           Id = 5
           Name = "Mushroom Lovers"
           Description = "It has mushrooms. Isn't that obvious?"
           BasePrice = 11.00m
           ImageUrl = "img/pizzas/mushroom.jpg"
       }
       {
           Id = 6
           Name = "The Brit"
           Description = "When in London..."
           BasePrice = 10.25m
           ImageUrl = "img/pizzas/brit.jpg"
       }
       {
           Id = 7
           Name = "Veggie Delight"
           Description = "It's like salad, but on a pizza"
           BasePrice = 11.50m
           ImageUrl = "img/pizzas/salad.jpg"
       }
       {
           Id = 8
           Name = "Margherita"
           Description = "Traditional Italian pizza with tomatoes and basil"
           BasePrice = 9.99m
           ImageUrl = "img/pizzas/margherita.jpg"
       }
   ]
let initialize (db : PizzaStoreContext) =

    db.Specials.AddRange specials
    db.SaveChanges() |> ignore

