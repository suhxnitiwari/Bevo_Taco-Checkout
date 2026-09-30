using BevosTacos.Data;
using BevosTacos.Models;
using BevosTacos.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BevosTacos.Controllers;

public class MenuController(AppDbContext db, CartService cart) : Controller
{
    public async Task<IActionResult> Index(MenuCategory category = MenuCategory.Tacos)
    {
        var items = await db.MenuItems
            .Include(m => m.AddOns.OrderBy(a => a.Price))
            .Where(m => m.Category == category)
            .OrderBy(m => m.SortOrder)
            .AsNoTracking()
            .ToListAsync();

        return View(new MenuViewModel { Items = items, Category = category });
    }

    [HttpPost]
    public async Task<IActionResult> Add(int menuItemId, int[] addOnIds, int quantity = 1)
    {
        var item = await db.MenuItems.AsNoTracking().FirstOrDefaultAsync(m => m.MenuItemId == menuItemId);
        if (item is null)
        {
            return NotFound();
        }

        if (!item.IsAvailable)
        {
            TempData["Error"] = $"Sorry, {item.Name} is sold out right now.";
        }
        else
        {
            cart.Add(menuItemId, addOnIds, quantity);
            TempData["Message"] = $"Added {Math.Clamp(quantity, 1, CartService.MaxQuantityPerLine)} × {item.Name} to your cart.";
        }

        return RedirectToAction(nameof(Index), new { category = item.Category });
    }
}
