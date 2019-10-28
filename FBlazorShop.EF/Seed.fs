module Seed

open FBlazorShop.EF
open FBlazorShop.App.Model

let specials =
    [ { Id = 0
        Name = "Basic Cheese Pizza"
        Description = "It's cheesy and delicious. Why wouldn't you want one?"
        BasePrice = 9.99m
        ImageUrl = "img/pizzas/cheese.jpg" }
      { Id = 2
        Name = "The Baconatorizor"
        Description = "It has EVERY kind of bacon"
        BasePrice = 11.99m
        ImageUrl = "img/pizzas/bacon.jpg" }

      { Id = 3
        Name = "Classic pepperoni"
        Description = "It's the pizza you grew up with, but Blazing hot!"
        BasePrice = 10.50m
        ImageUrl = "img/pizzas/pepperoni.jpg" }
      { Id = 4
        Name = "Buffalo chicken"
        Description = "Spicy chicken, hot sauce and bleu cheese, guaranteed to warm you up"
        BasePrice = 12.75m
        ImageUrl = "img/pizzas/meaty.jpg" }
      { Id = 5
        Name = "Mushroom Lovers"
        Description = "It has mushrooms. Isn't that obvious?"
        BasePrice = 11.00m
        ImageUrl = "img/pizzas/mushroom.jpg" }
      { Id = 6
        Name = "The Brit"
        Description = "When in London..."
        BasePrice = 10.25m
        ImageUrl = "img/pizzas/brit.jpg" }
      { Id = 7
        Name = "Veggie Delight"
        Description = "It's like salad, but on a pizza"
        BasePrice = 11.50m
        ImageUrl = "img/pizzas/salad.jpg" }
      { Id = 8
        Name = "Margherita"
        Description = "Traditional Italian pizza with tomatoes and basil"
        BasePrice = 9.99m
        ImageUrl = "img/pizzas/margherita.jpg" } ]

let toppings =
    [ { Id = 0
        Name = "Extra cheese"
        Price = 2.50m }

      { Id = 0
        Name = "American bacon"
        Price = 2.99m }

      { Id = 0
        Name = "British bacon"
        Price = 2.99m }

      { Id = 0
        Name = "Canadian bacon"
        Price = 2.99m }

      { Id = 0
        Name = "Tea and crumpets"
        Price = 5.00m }

      { Id = 0
        Name = "Fresh-baked scones"
        Price = 4.50m }

      { Id = 0
        Name = "Bell peppers"
        Price = 1.00m }

      { Id = 0
        Name = "Onions"
        Price = 1.00m }

      { Id = 0
        Name = "Mushrooms"
        Price = 1.00m }

      { Id = 0
        Name = "Pepperoni"
        Price = 1.00m }

      { Id = 0
        Name = "Duck sausage"
        Price = 3.20m }

      { Id = 0
        Name = "Venison meatballs"
        Price = 2.50m }

      { Id = 0
        Name = "Served on a silver platter"
        Price = 250.99m }

      { Id = 0
        Name = "Lobster on top"
        Price = 64.50m }

      { Id = 0
        Name = "Sturgeon caviar"
        Price = 101.75m }

      { Id = 0
        Name = "Artichoke hearts"
        Price = 3.40m }

      { Id = 0
        Name = "Fresh tomatos"
        Price = 1.50m }

      { Id = 0
        Name = "Basil"
        Price = 1.50m }

      { Id = 0
        Name = "Steak (medium-rare)"
        Price = 8.50m }

      { Id = 0
        Name = "Blazing hot peppers"
        Price = 4.20m }

      { Id = 0
        Name = "Buffalo chicken"
        Price = 5.00m }

      { Id = 0
        Name = "Blue cheese"
        Price = 2.50m } ]

let initialize (db: PizzaStoreContext) =

    db.Specials.AddRange specials
    db.Toppings.AddRange toppings
    db.SaveChanges() |> ignore
