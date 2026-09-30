using System.Globalization;
using System.Text;
using BevosTacos.Data;
using BevosTacos.Models;
using BevosTacos.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BevosTacos.Controllers;

[Authorize(Roles = Roles.Manager)]
public class ManagerController(AppDbContext db, TimeProvider clock) : Controller
{
    public async Task<IActionResult> Index(int days = 14)
    {
        days = days is 7 or 14 or 30 ? days : 14;
        var today = AustinTime.Today(clock);
        var firstDay = today.AddDays(-(days - 1));
        var all = await OrdersSinceAsync(firstDay);

        //Cancelled orders don't count toward sales
        var sales = all.Where(o => o.Status != OrderStatus.Cancelled).ToList();
        var walkups = sales.OfType<WalkupOrder>().ToList();
        var catering = sales.OfType<CateringOrder>().ToList();
        var lines = sales.SelectMany(o => o.Lines).ToList();

        var daily = Enumerable.Range(0, days).Select(i =>
        {
            var day = firstDay.AddDays(i);
            bool OnDay(Order o) => DateOnly.FromDateTime(AustinTime.ToLocal(o.PlacedAtUtc)) == day;
            return new DailyRevenue(day, walkups.Where(OnDay).Sum(o => o.Total), catering.Where(OnDay).Sum(o => o.Total));
        }).ToList();

        var model = new DashboardViewModel
        {
            Days = days,
            Revenue = sales.Sum(o => o.Total),
            OrderCount = sales.Count,
            AverageTicket = sales.Count == 0 ? 0 : Math.Round(sales.Average(o => o.Total), 2),
            ItemsSold = lines.Sum(l => l.Quantity),
            SalesTax = walkups.Sum(o => o.SalesTax),
            Tips = walkups.Sum(o => o.Tip),
            FreeDeliveries = catering.Count(o => o.DeliveryWaivedReason is not null),
            DeliveryFeesWaived = catering.Where(o => o.DeliveryWaivedReason is not null).Sum(o => o.RequestedDeliveryFee),
            Cancelled = all.Count(o => o.Status == OrderStatus.Cancelled),
            WalkupRevenue = walkups.Sum(o => o.Total),
            CateringRevenue = catering.Sum(o => o.Total),
            WalkupOrders = walkups.Count,
            CateringOrders = catering.Count,
            Daily = daily,
            TopItems = lines.GroupBy(l => l.ItemName)
                .Select(g => new ItemSales(g.Key, g.Sum(l => l.Quantity), g.Sum(l => l.LineTotal)))
                .OrderByDescending(s => s.Quantity).ThenBy(s => s.Name)
                .Take(6).ToList(),
            ByCategory = Enum.GetValues<MenuCategory>().ToDictionary(c => c, c => lines.Where(l => l.Category == c).Sum(l => l.LineTotal)),
            Recent = all.OrderByDescending(o => o.PlacedAtUtc).Take(12).ToList()
        };

        return View(model);
    }

    //Every order in the date range as a spreadsheet
    public async Task<IActionResult> Export(int days = 14)
    {
        var firstDay = AustinTime.Today(clock).AddDays(-(Math.Clamp(days, 1, 365) - 1));
        var all = await OrdersSinceAsync(firstDay);

        static string Csv(string? value) => value is not null && value.IndexOfAny([',', '"', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value ?? "";
        static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        var csv = new StringBuilder("Order,Placed,Type,Customer,Items,Subtotal,Tax,Tip,Delivery,Total,Status\n");
        foreach (var o in all.OrderBy(o => o.PlacedAtUtc))
        {
            var (customer, tax, tip, delivery) = o switch
            {
                WalkupOrder w => (w.CustomerName ?? "Walk-up guest", Money(w.SalesTax), Money(w.Tip), ""),
                CateringOrder c => (c.CustomerCode, "", "", Money(c.DeliveryFee)),
                _ => ("", "", "", "")
            };
            csv.AppendLine(string.Join(',', o.OrderNumber, AustinTime.ToLocal(o.PlacedAtUtc).ToString("yyyy-MM-dd HH:mm"), o.CustomerType, Csv(customer),
                o.TotalItems, Money(o.Subtotal), tax, tip, delivery, Money(o.Total), o.Status));
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"bevos-tacos-orders-{AustinTime.Today(clock):yyyy-MM-dd}.csv");
    }

    public async Task<IActionResult> Menu()
    {
        var items = await db.MenuItems.OrderBy(m => m.SortOrder).AsNoTracking().ToListAsync();
        return View(items);
    }

    //Changes an item's price or marks it sold out; past orders keep the price they were charged
    [HttpPost]
    public async Task<IActionResult> UpdateItem(int id, decimal price, bool isAvailable)
    {
        var item = await db.MenuItems.FindAsync(id);
        if (item is null) return NotFound();

        if (price is < 0.25m or > 100m)
        {
            TempData["Error"] = "Price must be between $0.25 and $100.";
            return RedirectToAction(nameof(Menu));
        }

        item.Price = Math.Round(price, 2);
        item.IsAvailable = isAvailable;
        await db.SaveChangesAsync();
        TempData["Message"] = $"Updated {item.Name}.";
        return RedirectToAction(nameof(Menu));
    }

    public async Task<IActionResult> Customers()
    {
        var customerRole = await db.Roles.Where(r => r.Name == Roles.Customer).Select(r => r.Id).FirstAsync();
        var customerIds = db.UserRoles.Where(ur => ur.RoleId == customerRole).Select(ur => ur.UserId);
        var customers = await db.Users.Where(u => customerIds.Contains(u.Id)).OrderBy(u => u.FirstName).AsNoTracking().ToListAsync();

        var totals = (await db.Orders.Where(o => o.CustomerId != null && o.Status != OrderStatus.Cancelled)
                .Select(o => new { o.CustomerId, o.Total }).ToListAsync())
            .GroupBy(o => o.CustomerId!)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Spent: g.Sum(o => o.Total)));

        return View(customers.Select(u => new CustomerRow
        {
            User = u,
            Orders = totals.GetValueOrDefault(u.Id).Count,
            Spent = totals.GetValueOrDefault(u.Id).Spent
        }).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> SetPreferred(string id, bool preferred)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        user.PreferredCustomer = preferred;
        await db.SaveChangesAsync();
        TempData["Message"] = $"{user.FirstName} is {(preferred ? "now" : "no longer")} a preferred customer.";
        return RedirectToAction(nameof(Customers));
    }

    //Loads in memory so money math works the same on SQLite (no decimal SUM) and Postgres
    private async Task<List<Order>> OrdersSinceAsync(DateOnly firstLocalDay)
    {
        var fromUtc = firstLocalDay.ToDateTime(TimeOnly.MinValue).AddDays(-1);
        var orders = await db.Orders.Include(o => o.Lines).Where(o => o.PlacedAtUtc >= fromUtc).AsNoTracking().ToListAsync();
        return orders.Where(o => DateOnly.FromDateTime(AustinTime.ToLocal(o.PlacedAtUtc)) >= firstLocalDay).ToList();
    }
}
