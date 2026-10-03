# Bevo's Tacos

*A full ordering system for an Austin food truck: menu and cart, walk-up and catering checkout, a kitchen board and a manager dashboard.*

[![CI](https://github.com/suhxnitiwari/Bevo_Taco-Checkout/actions/workflows/ci.yml/badge.svg)](https://github.com/suhxnitiwari/Bevo_Taco-Checkout/actions/workflows/ci.yml)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![ASP.NET Core MVC](https://img.shields.io/badge/ASP.NET%20Core-MVC-512BD4)
![EF Core](https://img.shields.io/badge/EF%20Core-PostgreSQL-336791)

**Live demo (static, no server):** https://suhxnitiwari.github.io/Bevo_Taco-Checkout/

![Home page](docs/screenshots/home.png)

## What it is

It started as MIS 333K Homework 2 at UT Austin, a two-page checkout that priced tacos and burgers to practice object-oriented programming and inheritance. I kept going until it became a working business app with three kinds of users. The pricing rules from that assignment are still the core of the system.

- **Customers** order from 13 menu items in four categories, with add-ons priced per item (guac, queso, double patty). Walk-up orders need no account; catering orders do.
- **Kitchen staff** move orders from New → Cooking → Ready → Complete. Customers can cancel until cooking starts.
- **Managers** change prices, mark items sold out, grant preferred-customer status, and read a sales dashboard: revenue by day, walk-up vs. catering, top sellers, sales by category, tax and tips collected, delivery fees waived, and CSV export.
- **Demo sign-in** gives visitors one-click accounts for each role.

## How it's built

**An inheritance model that does the pricing.** One `Orders` table (table-per-hierarchy with an `OrderType` discriminator) holds two subclasses of an abstract `Order`:

```
Order (abstract)          CalcTotals(): sums the lines (EmptyOrderException if none), then calls CalcTotal()
├── WalkupOrder           subtotal + 8.25% tax + tip, each rounded half away from zero to the cent
└── CateringOrder         subtotal + delivery fee by zone ($50–$250), waived for preferred
                          customers or orders of $1,000+; event date needs 2 days' notice
```

`CalcTotals()` runs the steps every order shares and leaves one step to each subclass (the template method pattern). Calculated amounts have private setters, so only the pricing logic can change them.

**Server-side rules you can't get around from the browser.**
- The cart stores only item IDs, add-on IDs and quantities. Every total is recalculated from database prices, and add-ons that don't belong to an item are rejected.
- Each order line snapshots the item name, add-ons and price, so a later price change never rewrites an old receipt.
- Preferred status lives on the account and only a manager can grant it; the catering form can't set it.
- ASP.NET Identity with role-based `[Authorize]` on the kitchen and manager pages. Customers can open only their own orders, and guests only the ones they placed that session.
- A global anti-forgery filter protects every form post.

**Tests and CI.** 77 xUnit tests: 49 unit tests (tax rounding, tips, add-on pricing, the free-delivery threshold, every validation rule, order status rules) and 28 integration tests that boot the real app with `WebApplicationFactory` against a throwaway database and check full flows, including server-side pricing, sold-out items, unchanged receipts after price edits, role access and anti-forgery. CI runs them on every push, applies the EF Core migrations to a real Postgres 17 container, and fails if the model has changes that aren't in a migration.

**Two databases, one codebase.** SQLite locally (created and seeded with the menu, demo accounts and two weeks of sample orders on first run), Postgres in production with migrations applied on startup. Data-protection keys are stored in the database so sign-in cookies survive redeploys, and a multi-stage `Dockerfile` builds the image for Render.

**A static twin for GitHub Pages.** The `web/` folder ports the same domain model to JavaScript (integer cents, tax in basis points) and saves orders in localStorage, so the demo works with no server. Its own 38 Node tests must pass before the Pages deploy runs.

## Design choices

- Dates and times are shown in Austin (Central) time, wherever the server runs.
- Receipts are printable and include a progress tracker that follows the kitchen status.
- In the static demo, the catering form suggests how many tacos to add from the guest count (three per guest). In both versions, validation messages sit next to the field that needs fixing.

![Walk-up checkout validation](docs/screenshots/walkup-error.png)
![Catering order summary](docs/screenshots/catering-summary.png)

## Tech stack

C# / .NET 10, ASP.NET Core MVC, Entity Framework Core (SQLite + PostgreSQL), ASP.NET Identity, Bootstrap, xUnit, GitHub Actions, Docker, vanilla JS for the static demo.

## Run it locally

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/BevosTacos
dotnet test
```

In production, set `DATABASE_URL` (or `ConnectionStrings__Postgres`) to point the app at Postgres.

## Project layout

```
src/BevosTacos/        Controllers, Models (order hierarchy), Data (context, seed, migrations), Services, Views
tests/BevosTacos.Tests unit tests, plus Integration/ tests over HTTP
web/                   the static JavaScript demo deployed to GitHub Pages
```

## Ownership

© 2026 Suhani Tiwari. **All rights reserved.** This is my original work. The code is public so you can see how I build, not so you can reuse it: copying, reusing or republishing any part of it, including for a portfolio or a class assignment, is not permitted without my written permission. See [LICENSE](LICENSE).

Built by [Suhani Tiwari](https://suhanitiwari.com).
