using BevosTacos.Data;
using BevosTacos.Models;
using BevosTacos.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BevosTacos.Controllers;

public class OrdersController(AppDbContext db, OrderService orders, UserManager<AppUser> users, TimeProvider clock) : Controller
{
    //The signed-in customer's order history
    [Authorize]
    public async Task<IActionResult> Index()
    {
        var userId = users.GetUserId(User);
        var mine = await db.Orders.Include(o => o.Lines)
            .Where(o => o.CustomerId == userId)
            .OrderByDescending(o => o.PlacedAtUtc)
            .AsNoTracking()
            .ToListAsync();
        return View(mine);
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await orders.FindAsync(id);
        if (order is null || !CanView(order))
        {
            return NotFound();
        }
        return View(order);
    }

    //Customers can cancel only until the kitchen starts cooking
    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var order = await orders.FindAsync(id);
        if (order is null || !CanView(order))
        {
            return NotFound();
        }

        if (order.Cancel(clock.GetUtcNow().UtcDateTime))
        {
            await db.SaveChangesAsync();
            TempData["Message"] = "Your order was cancelled.";
        }
        else
        {
            TempData["Error"] = "This order can't be cancelled because the kitchen already started it.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    //Staff see every order; customers see their own; guests see what they placed this session
    private bool CanView(Order order)
    {
        if (User.IsInRole(Roles.Kitchen) || User.IsInRole(Roles.Manager)) return true;
        if (order.CustomerId is not null && order.CustomerId == users.GetUserId(User)) return true;
        var guestOrders = HttpContext.Session.GetString(CheckoutController.GuestOrdersKey)?.Split(',') ?? [];
        return guestOrders.Contains(order.OrderId.ToString());
    }
}
