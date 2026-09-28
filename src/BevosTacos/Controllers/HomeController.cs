using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using BevosTacos.Models;

namespace BevosTacos.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult CheckoutWalkup()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult WalkupTotals(WalkupOrder walkupOrder)
    {
        return Checkout(walkupOrder, nameof(CheckoutWalkup));
    }

    public IActionResult CheckoutCatering()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CateringTotals(CateringOrder cateringOrder)
    {
        return Checkout(cateringOrder, nameof(CheckoutCatering));
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    //Shared checkout flow: validate the form, price the order, then show the summary
    //or send the customer back to the form with the errors
    private IActionResult Checkout(Order order, string formView)
    {
        if (!ModelState.IsValid)
        {
            return View(formView, order);
        }

        try
        {
            order.CalcTotals();
        }
        catch (EmptyOrderException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(formView, order);
        }

        return View(order);
    }
}
