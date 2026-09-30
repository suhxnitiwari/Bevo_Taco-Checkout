using BevosTacos.Models;
using BevosTacos.Services;
using Microsoft.AspNetCore.Mvc;

namespace BevosTacos.Controllers;

public class CartController(CartService cart, OrderService orders) : Controller
{
    public async Task<IActionResult> Index()
    {
        var lines = cart.GetLines();
        return View(new CartViewModel { Cart = lines, Priced = await orders.PriceCartAsync(lines) });
    }

    [HttpPost]
    public IActionResult Update(string key, int quantity)
    {
        cart.SetQuantity(key, quantity);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Clear()
    {
        cart.Clear();
        return RedirectToAction(nameof(Index));
    }
}
