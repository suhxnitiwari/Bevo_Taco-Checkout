# Bevo's Tacos Checkout

[![CI](https://github.com/suhxnitiwari/Bevo_Taco-Checkout/actions/workflows/ci.yml/badge.svg)](https://github.com/suhxnitiwari/Bevo_Taco-Checkout/actions/workflows/ci.yml)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![ASP.NET Core MVC](https://img.shields.io/badge/ASP.NET%20Core-MVC-512BD4)
![Bootstrap 5](https://img.shields.io/badge/Bootstrap-5-7952B3)

A checkout app for an Austin food truck, built with ASP.NET Core MVC. It prices walk-up and catering orders with different rules: sales tax for walk-up customers, and delivery fees with free-delivery rules for catering customers.

Built for MIS 372T (Object-Oriented Programming and Inheritance) at UT Austin.

![Home page](docs/screenshots/home.png)

| Catering order summary | Validation error |
| --- | --- |
| ![Catering order summary](docs/screenshots/catering-summary.png) | ![Validation error](docs/screenshots/walkup-error.png) |

## Features

- **Walk-up orders:** tacos ($2.75) and burgers ($4.50), plus 8.25% sales tax rounded to the cent.
- **Catering orders:** a 2–4 letter customer code and a delivery fee from $0 to $250. Delivery is free for preferred customers and for orders of $1,000 or more.
- **Validation in two places:** data annotations on the models drive both the browser checks (jQuery Unobtrusive Validation) and the server checks (`ModelState`), so the rules live in one spot.
- **Business rules in the domain model:** an order with no items throws from the base class, and the controller turns that into a friendly error message.
- **Unit tests:** xUnit tests cover pricing, tax rounding, the $1,000 free-delivery threshold, and every validation rule. They run on each push through GitHub Actions.

## Design

```
Order (abstract)                    constants TACO_PRICE, BURGER_PRICE
│  CustomerType, item counts,       CalcSubtotals(): shared subtotal logic,
│  subtotals, Total                 throws if the order is empty
│
├── WalkupOrder                     SALES_TAX_RATE, CustomerName, SalesTax
│     CalcTotals() → subtotal + tax
│
└── CateringOrder                   CustomerCode, DeliveryFee, PreferredCustomer
      CalcTotals() → subtotal + delivery fee (waived when preferred or ≥ $1,000)
```

The models hold all pricing and validation logic and don't reference views. The controller stays thin: it binds the form, checks `ModelState`, calls `CalcTotals()`, and picks a view.

## Project layout

```
Controllers/HomeController.cs     checkout forms and totals actions
Models/                           Order, WalkupOrder, CateringOrder
Views/Home/                       home, checkout, and summary pages
wwwroot/                          styles, images, client libraries
tests/Tiwari_Suhani_HW2.Tests/    xUnit tests for the models
```

## Running locally

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project Tiwari_Suhani_HW2.csproj   # start the app
dotnet test                                     # run the unit tests
```

## Author

**Suhani Tiwari**, University of Texas at Austin
