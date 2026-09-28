using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Tiwari_Suhani_HW2.Models;

namespace Tiwari_Suhani_HW2.Controllers;

//Name: Suhani Tiwari
//Date: 09/25/2026
//Description: HW2 - Food Truck Checkout
public class HomeController : Controller
{
    //Displays the home page (Views/Home/Index.cshtml)
    public IActionResult Index()
    {
        return View();
    }

    //Displays the catering checkout form (Views/Home/CheckoutCatering.cshtml)
    public IActionResult CheckoutCatering()
    {
        return View();
    }

    //Receives the submitted catering order, validates it, calculates totals, and displays the results
    [HttpPost]
    public IActionResult CateringTotals(CateringOrder cateringOrder)
    {
        //If the submitted data doesn't meet the validation rules (data annotations), send the customer back to the checkout form
        if (!ModelState.IsValid)
        {
            return View("CheckoutCatering", cateringOrder);
        }

        //Data is valid - set the customer type and calculate the order totals
        cateringOrder.CustomerType = CustomerType.Catering;

        try
        {
            cateringOrder.CalcTotals();
        }
        catch (Exception ex)
        {
            //Business rule was violated (e.g. no tacos or burgers ordered) - show the message and send the customer back to the form
            //ex.Message is the "wrapper" message thrown by CalcTotals; ex.InnerException.Message is the original
            //message thrown by CalcSubtotals (e.g. "Order must contain at least one taco or burger.")
            ViewBag.Error = ex.Message + " " + ex.InnerException?.Message;
            return View("CheckoutCatering", cateringOrder);
        }

        return View(cateringOrder);
    }

    //Displays the walkup checkout form (Views/Home/CheckoutWalkup.cshtml)
    public IActionResult CheckoutWalkup()
    {
        return View();
    }

    //Receives the submitted walkup order, validates it, calculates totals, and displays the results
    [HttpPost]
    public IActionResult WalkupTotals(WalkupOrder walkupOrder)
    {
        //If the submitted data doesn't meet the validation rules (data annotations), send the customer back to the checkout form
        if (!ModelState.IsValid)
        {
            return View("CheckoutWalkup", walkupOrder);
        }

        //Data is valid - set the customer type and calculate the order totals
        walkupOrder.CustomerType = CustomerType.Walkup;

        try
        {
            walkupOrder.CalcTotals();
        }
        catch (Exception ex)
        {
            //Business rule was violated (e.g. no tacos or burgers ordered) - show the message and send the customer back to the form
            //ex.Message is the "wrapper" message thrown by CalcTotals; ex.InnerException.Message is the original
            //message thrown by CalcSubtotals (e.g. "Order must contain at least one taco or burger.")
            ViewBag.Error = ex.Message + " " + ex.InnerException?.Message;
            return View("CheckoutWalkup", walkupOrder);
        }

        return View(walkupOrder);
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
}
