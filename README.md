# Bevo's Tacos

[![CI](https://github.com/suhxnitiwari/Bevo_Taco-Checkout/actions/workflows/ci.yml/badge.svg)](https://github.com/suhxnitiwari/Bevo_Taco-Checkout/actions/workflows/ci.yml)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![ASP.NET Core MVC](https://img.shields.io/badge/ASP.NET%20Core-MVC-512BD4)
![EF Core](https://img.shields.io/badge/EF%20Core-PostgreSQL-336791)

## Ownership

© 2026 Suhani Tiwari. **All rights reserved.** This is my original work. The code is public so you can see how I build, not so you can reuse it: copying, reusing or republishing any part of it, including for a portfolio or a class assignment, is not permitted without my written permission. See [LICENSE](LICENSE).

An ordering system for an Austin food truck, built with ASP.NET Core MVC, Entity Framework Core and ASP.NET Identity. Customers order from a full menu, the kitchen works orders on a live board, and managers run the business from a sales dashboard.

It started as MIS 333K Homework 2 at UT Austin (object-oriented programming and inheritance), a two-page checkout that priced tacos and burgers. The pricing rules from that assignment are still the core of the system.

**Static demo (no server): https://suhxnitiwari.github.io/Bevo_Taco-Checkout/**

## Features

- **Menu and cart:** 13 items in four categories, with add-ons priced per item (guac, queso, double patty). Managers can change prices and mark items sold out.
- **Walk-up checkout:** no account needed. 8.25% sales tax, rounded half away from zero to the cent, plus an optional tip.
- **Catering checkout:** for signed-in customers. Delivery fee by zone ($50–$250), waived for preferred customers and for orders of $1,000 or more, with 2 days' notice required.
- **Three roles:** customers see their own orders, kitchen staff move orders from New → Cooking → Ready → Complete, and managers see everything.
- **Sales dashboard:** revenue by day, walk-up vs. catering, top sellers, sales by category, tax and tips collected, delivery fees waived, and CSV export.
- **Receipts:** printable, with a progress tracker. Customers can cancel until the kitchen starts cooking.
- **Demo sign-in:** one-click accounts for each role, so visitors can try the kitchen and manager views.

## Design

```
Order (abstract)                    one Orders table (table-per-hierarchy), OrderType discriminator
│  CalcTotals()                     sums the order lines (EmptyOrderException if there are none),
│                                   then calls CalcTotal()
│  abstract CalcTotal()             each order type adds its own charges
│  Advance() / Cancel()             the kitchen status rules
│
├── WalkupOrder                     CustomerName, TipPercent, SalesTax, Tip
│     CalcTotal() → subtotal + 8.25% tax + tip
│
└── CateringOrder                   CustomerCode, DeliveryFee, PreferredCustomer, EventDate, GuestCount
      CalcTotal() → subtotal + delivery fee (waived when preferred or ≥ $1,000)
      Validate()  → event date needs 2 days' notice
```

`CalcTotals()` runs the steps every order shares and leaves one step for each subclass (the template method pattern). Calculated amounts have private setters, so only the pricing logic can change them.

Security and data rules:

- **Prices come from the database, not the browser.** The cart stores only item IDs, add-on IDs and quantities, and every total is recalculated on the server. Add-ons that don't belong to an item are rejected.
- **Receipts don't change.** Each order line copies the item name, add-ons and price when the order is placed, so later menu changes never rewrite an old receipt.
- **Preferred status comes from the account.** Only a manager can grant it, and the catering form can't set it.
- **Access is checked on the server.** Role-based `[Authorize]` attributes protect the kitchen and manager pages. Customers can only open their own orders, and guests only the ones they placed that session.
- **Every form post is protected** by an anti-forgery token (a global filter).

## Project layout

```
src/BevosTacos/
  Controllers/        Menu, Cart, Checkout, Orders, Kitchen, Manager, Account
  Models/             Order hierarchy, MenuItem, AddOn, OrderLine, AppUser, view models
  Data/               AppDbContext, seed data, Postgres migrations
  Services/           CartService (session cart), OrderService (pricing and placing orders)
  Views/              Razor views
tests/BevosTacos.Tests/
  *Tests.cs           unit tests for pricing, validation and order status
  Integration/        tests that run the real app over HTTP against a throwaway database
web/                  the static JavaScript demo deployed to GitHub Pages
```

## Running locally

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). Locally the app uses a SQLite file, creates it on first run, and fills it with the menu, demo accounts and two weeks of sample orders.

```bash
dotnet run --project src/BevosTacos
```

```bash
dotnet test
```

In production, set `DATABASE_URL` (or `ConnectionStrings__Postgres`) and the app applies the EF Core migrations to Postgres on startup. The `Dockerfile` builds the image for Render.

## Tests

- **49 unit tests** cover tax rounding, tips, add-on pricing, the free-delivery threshold, every validation rule, and the order status rules.
- **28 integration tests** start the app with `WebApplicationFactory` and check full flows: placing orders, server-side pricing, rejected add-ons, sold-out items, receipts staying unchanged after price edits, role access, customers seeing only their own orders, and anti-forgery protection.
- **CI** runs every test on each push, applies the migrations to a real Postgres 17 container, and fails if the model has changes that aren't in a migration.

## Author

**Suhani Tiwari**, University of Texas at Austin
