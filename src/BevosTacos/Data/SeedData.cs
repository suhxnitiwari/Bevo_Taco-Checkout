using BevosTacos.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BevosTacos.Data;

//Fills an empty database with the menu, the three roles, demo accounts and two weeks of orders
public static class SeedData
{
    public const string DemoCustomerEmail = "maya@demo.bevostacos.test";
    public const string DemoPreferredEmail = "longhorn-events@demo.bevostacos.test";
    public const string DemoKitchenEmail = "kitchen@demo.bevostacos.test";
    public const string DemoManagerEmail = "manager@demo.bevostacos.test";

    public static async Task InitializeAsync(IServiceProvider services, bool includeSampleOrders)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var clock = services.GetRequiredService<TimeProvider>();

        foreach (var role in new[] { Roles.Customer, Roles.Kitchen, Roles.Manager })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureUserAsync(userManager, DemoCustomerEmail, "Maya", Roles.Customer, "MISA", preferred: false);
        await EnsureUserAsync(userManager, DemoPreferredEmail, "Longhorn Events", Roles.Customer, "UTX", preferred: true);
        await EnsureUserAsync(userManager, DemoKitchenEmail, "Kitchen", Roles.Kitchen, null, preferred: false);
        await EnsureUserAsync(userManager, DemoManagerEmail, "Manager", Roles.Manager, null, preferred: false);

        if (!await db.MenuItems.AnyAsync())
        {
            SeedMenu(db);
            await db.SaveChangesAsync();
        }

        if (includeSampleOrders && !await db.Orders.AnyAsync())
        {
            await SeedOrdersAsync(db, userManager, clock.GetUtcNow().UtcDateTime);
        }

        if (includeSampleOrders)
        {
            await EnsureLiveOrdersAsync(db, clock.GetUtcNow().UtcDateTime);
        }
    }

    //Demo accounts get a random password nobody knows; they're only reachable through demo sign-in
    private static async Task EnsureUserAsync(UserManager<AppUser> users, string email, string firstName, string role, string? code, bool preferred)
    {
        if (await users.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, FirstName = firstName, CustomerCode = code, PreferredCustomer = preferred };
        var result = await users.CreateAsync(user, $"Demo-{Guid.NewGuid():N}!");
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not create {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
        await users.AddToRoleAsync(user, role);
    }

    private static void SeedMenu(AppDbContext db)
    {
        var guac = new AddOn { Name = "Add guac", Price = 0.75m };
        var queso = new AddOn { Name = "Add queso", Price = 0.50m };
        var flour = new AddOn { Name = "Flour tortilla", Price = 0m };
        var doublePatty = new AddOn { Name = "Double patty", Price = 2.00m };
        var large = new AddOn { Name = "Large size", Price = 1.00m };

        int sort = 0;
        MenuItem Item(string name, string desc, MenuCategory cat, decimal price, string emoji, bool veg = false, bool spicy = false, params AddOn[] addOns) =>
            new() { Name = name, Description = desc, Category = cat, Price = price, Emoji = emoji, IsVegetarian = veg, IsSpicy = spicy, SortOrder = sort++, AddOns = addOns.ToList() };

        db.MenuItems.AddRange(
            Item("Smoked Brisket", "12-hour post oak brisket, pickled onion, salsa verde", MenuCategory.Tacos, 2.75m, "🌮", addOns: [guac, queso, flour]),
            Item("Al Pastor", "Achiote pork, grilled pineapple, cilantro, onion", MenuCategory.Tacos, 2.75m, "🌮", addOns: [guac, queso, flour]),
            Item("Migas", "Scrambled eggs, tortilla strips, jack cheese, pico", MenuCategory.Tacos, 2.75m, "🍳", veg: true, addOns: [guac, queso, flour]),
            Item("Roasted Veggie", "Poblano, sweet potato, black beans, cotija", MenuCategory.Tacos, 2.75m, "🥑", veg: true, addOns: [guac, queso, flour]),
            Item("Classic Longhorn", "American cheese, pickles, onion, house sauce", MenuCategory.Burgers, 4.50m, "🍔", addOns: [guac, queso, doublePatty]),
            Item("Hatch Green Chile", "Roasted Hatch chiles, pepper jack, lime crema", MenuCategory.Burgers, 4.50m, "🌶️", spicy: true, addOns: [guac, queso, doublePatty]),
            Item("Portobello", "Grilled portobello cap, swiss, caramelized onion", MenuCategory.Burgers, 4.50m, "🍄", veg: true, addOns: [guac, queso]),
            Item("Chips & Queso", "Fresh chips with Bevo's white queso", MenuCategory.Sides, 3.25m, "🧀", veg: true),
            Item("Street Elote", "Charred corn, mayo, cotija, chile-lime", MenuCategory.Sides, 3.50m, "🌽", veg: true),
            Item("Chips & Guac", "Smashed to order", MenuCategory.Sides, 4.00m, "🥑", veg: true),
            Item("Horchata", "Cinnamon rice milk over ice", MenuCategory.Drinks, 2.50m, "🥛", addOns: [large]),
            Item("Topo Chico", "The official water of Austin", MenuCategory.Drinks, 2.00m, "🫧"),
            Item("Sweet Tea", "Brewed every morning", MenuCategory.Drinks, 2.00m, "🧋", addOns: [large]));
    }

    //Two weeks of realistic orders so the dashboard has something to show.
    //A fixed random seed keeps the demo data the same every time.
    private static async Task SeedOrdersAsync(AppDbContext db, UserManager<AppUser> users, DateTime nowUtc)
    {
        var rng = new Random(333);
        var menu = await db.MenuItems.Include(m => m.AddOns).OrderBy(m => m.SortOrder).ToListAsync();
        var maya = await users.FindByEmailAsync(DemoCustomerEmail);
        var events = await users.FindByEmailAsync(DemoPreferredEmail);
        string[] names = ["Jordan", "Priya", "Sam", "Alex", "Diego", "Hannah", "Marcus", "Lena", "Chris", ""];

        //Walk-ups lean on tacos and burgers; catering is tacos and burgers by the dozen
        var mains = menu.Where(m => m.Category is MenuCategory.Tacos or MenuCategory.Burgers).ToList();
        OrderLine RandomLine(int maxQty) => Line(menu[rng.Next(menu.Count)], rng.Next(1, maxQty + 1));
        OrderLine CateringLine() => Line(mains[rng.Next(mains.Count)], 12 * rng.Next(2, 9));

        OrderLine Line(MenuItem item, int quantity)
        {
            var addOns = item.AddOns.Where(_ => rng.NextDouble() < 0.25).ToList();
            return new OrderLine
            {
                MenuItemId = item.MenuItemId,
                ItemName = item.Name,
                Category = item.Category,
                AddOnSummary = string.Join(", ", addOns.Select(a => a.Name)),
                UnitPrice = item.Price + addOns.Sum(a => a.Price),
                Quantity = quantity
            };
        }

        var orders = new List<Order>();
        for (int daysAgo = 13; daysAgo >= 0; daysAgo--)
        {
            //Busier on weekends, like a real truck
            var day = nowUtc.Date.AddDays(-daysAgo);
            int walkups = day.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday ? rng.Next(22, 32) : rng.Next(10, 18);
            for (int i = 0; i < walkups; i++)
            {
                var placed = day.AddHours(16 + rng.Next(0, 10)).AddMinutes(rng.Next(60)); //11am-9pm Central in UTC
                if (placed > nowUtc) continue;
                var order = new WalkupOrder
                {
                    CustomerName = names[rng.Next(names.Length)],
                    TipPercent = WalkupOrder.TipOptions[rng.Next(WalkupOrder.TipOptions.Length)],
                    PlacedAtUtc = placed,
                    CustomerId = rng.NextDouble() < 0.15 ? maya?.Id : null,
                    Lines = [Line(mains[rng.Next(mains.Count)], rng.Next(1, 4)), .. Enumerable.Range(0, rng.Next(0, 3)).Select(_ => RandomLine(2))]
                };
                orders.Add(order);
            }

            if (daysAgo % 3 == 1)
            {
                var customer = rng.NextDouble() < 0.5 ? events : maya;
                var order = new CateringOrder
                {
                    CustomerId = customer?.Id,
                    CustomerCode = customer?.CustomerCode ?? "MISA",
                    PreferredCustomer = customer?.PreferredCustomer ?? false,
                    DeliveryFee = new[] { 75m, 100m, 150m }[rng.Next(3)],
                    GuestCount = rng.Next(20, 120),
                    EventDate = DateOnly.FromDateTime(day.AddDays(3)),
                    DeliveryAddress = "2110 Speedway, Austin, TX 78712",
                    PlacedAtUtc = day.AddHours(15),
                    Lines = [.. Enumerable.Range(0, rng.Next(2, 5)).Select(_ => CateringLine()), RandomLine(20)]
                };
                orders.Add(order);
            }
        }

        foreach (var order in orders)
        {
            order.CalcTotals();
            var age = nowUtc - order.PlacedAtUtc;
            //Older orders are done; the last hour's orders are still moving through the kitchen
            order.Status = age.TotalMinutes switch
            {
                > 90 => rng.NextDouble() < 0.04 ? OrderStatus.Cancelled : OrderStatus.Complete,
                > 45 => OrderStatus.Ready,
                > 15 => OrderStatus.Cooking,
                _ => OrderStatus.New
            };
            order.StatusChangedAtUtc = order.Status == OrderStatus.New ? order.PlacedAtUtc : order.PlacedAtUtc.AddMinutes(10);
        }

        db.Orders.AddRange(orders);
        await db.SaveChangesAsync();
    }

    //Keeps the demo kitchen board from sitting empty: if nothing is in progress when the
    //app starts, a few walk-up orders from the last half hour are added at different stages.
    private static async Task EnsureLiveOrdersAsync(AppDbContext db, DateTime nowUtc)
    {
        if (await db.Orders.AnyAsync(o => o.Status == OrderStatus.New || o.Status == OrderStatus.Cooking || o.Status == OrderStatus.Ready))
        {
            return;
        }

        var menu = await db.MenuItems.Where(m => m.IsAvailable).OrderBy(m => m.SortOrder).ToListAsync();
        if (menu.Count == 0) return;

        OrderLine Line(int index, int quantity)
        {
            var item = menu[index % menu.Count];
            return new OrderLine { MenuItemId = item.MenuItemId, ItemName = item.Name, Category = item.Category, UnitPrice = item.Price, Quantity = quantity };
        }

        var live = new (string Name, int Tip, int MinutesAgo, OrderStatus Status, OrderLine[] Lines)[]
        {
            ("Jordan", 18, 28, OrderStatus.Ready, [Line(0, 2), Line(11, 1)]),
            ("Priya", 20, 19, OrderStatus.Cooking, [Line(3, 3), Line(8, 1)]),
            ("Sam", 15, 11, OrderStatus.Cooking, [Line(4, 1), Line(5, 1), Line(7, 1)]),
            ("", 0, 4, OrderStatus.New, [Line(1, 4), Line(12, 2)]),
            ("Alex", 18, 1, OrderStatus.New, [Line(6, 1), Line(10, 1)])
        };

        foreach (var (name, tip, minutesAgo, status, lines) in live)
        {
            var order = new WalkupOrder { CustomerName = name == "" ? null : name, TipPercent = tip, Lines = lines.ToList() };
            order.CalcTotals();
            order.PlacedAtUtc = nowUtc.AddMinutes(-minutesAgo);
            order.Status = status;
            order.StatusChangedAtUtc = status == OrderStatus.New ? order.PlacedAtUtc : nowUtc.AddMinutes(-Math.Min(minutesAgo, 6));
            db.Orders.Add(order);
        }
        await db.SaveChangesAsync();
    }
}
