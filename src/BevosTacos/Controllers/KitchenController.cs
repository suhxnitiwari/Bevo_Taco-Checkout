using BevosTacos.Data;
using BevosTacos.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BevosTacos.Controllers;

[Authorize(Roles = Roles.Staff)]
public class KitchenController(AppDbContext db, TimeProvider clock) : Controller
{
    //Open orders plus anything finished in the last hour
    public async Task<IActionResult> Index()
    {
        var recent = clock.GetUtcNow().UtcDateTime.AddHours(-1);
        var board = await db.Orders.Include(o => o.Lines)
            .Where(o => o.Status == OrderStatus.New || o.Status == OrderStatus.Cooking || o.Status == OrderStatus.Ready
                        || (o.Status == OrderStatus.Complete && o.StatusChangedAtUtc >= recent))
            .OrderBy(o => o.PlacedAtUtc)
            .AsNoTracking()
            .ToListAsync();
        return View(new KitchenViewModel { Orders = board });
    }

    [HttpPost]
    public async Task<IActionResult> Advance(int id)
    {
        var order = await db.Orders.FindAsync(id);
        if (order is null) return NotFound();

        if (order.Advance(clock.GetUtcNow().UtcDateTime))
        {
            await db.SaveChangesAsync();
            TempData["Message"] = $"Order #{order.OrderNumber} is now {order.Status}.";
        }
        return RedirectToAction(nameof(Index));
    }
}
