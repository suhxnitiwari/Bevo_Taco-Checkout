# Bevo's Tacos – Food Truck Checkout (HW2)

An ASP.NET Core MVC app (.NET 10) for checking out food truck orders.

## Features
- **Walk-up orders**: tacos ($2.75) and burgers ($4.50), plus 8.25% sales tax.
- **Catering orders**: customer code (2–4 letters) and a delivery fee ($0–$250). Delivery is free for preferred customers and for orders of $1,000 or more.
- Every order needs at least one item.
- Validation runs in the browser and on the server (data annotations).

## Project layout
- `Models/Order.cs`: abstract base class with shared pricing and subtotal logic
- `Models/WalkupOrder.cs`, `Models/CateringOrder.cs`: order-specific totals
- `Controllers/HomeController.cs`: checkout forms and totals actions
- `Views/Home/`: home, checkout, and summary pages

## Run
```bash
dotnet run
```

Author: Suhani Tiwari
