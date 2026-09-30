using BevosTacos.Models;
using BevosTacos.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BevosTacos.Controllers;

public class CheckoutController(CartService cart, OrderService orders, UserManager<AppUser> users, TimeProvider clock) : Controller
{
    //Guests who order without an account can still see their own receipts this session
    public const string GuestOrdersKey = "guest-orders";

    private static readonly HashSet<string> FormFields =
        [nameof(WalkupOrder.CustomerName), nameof(WalkupOrder.TipPercent), nameof(CateringOrder.EventDate), nameof(CateringOrder.GuestCount), nameof(CateringOrder.DeliveryAddress)];

    public async Task<IActionResult> Walkup()
    {
        var priced = await orders.PriceCartAsync(cart.GetLines());
        if (priced.Lines.Count == 0) return RedirectToAction("Index", "Cart");
        return View(new WalkupCheckoutViewModel { Priced = priced });
    }

    [HttpPost]
    public async Task<IActionResult> Walkup(WalkupCheckoutViewModel input)
    {
        var priced = await orders.PriceCartAsync(cart.GetLines());
        var order = new WalkupOrder
        {
            CustomerName = string.IsNullOrWhiteSpace(input.CustomerName) ? null : input.CustomerName.Trim(),
            TipPercent = input.TipPercent,
            CustomerId = users.GetUserId(User)
        };

        return await Place(order, priced, () => View(new WalkupCheckoutViewModel { CustomerName = input.CustomerName, TipPercent = input.TipPercent, Priced = priced }));
    }

    [Authorize(Roles = Roles.Customer)]
    public async Task<IActionResult> Catering()
    {
        var priced = await orders.PriceCartAsync(cart.GetLines());
        if (priced.Lines.Count == 0) return RedirectToAction("Index", "Cart");
        return View(await CateringModel(new CateringCheckoutViewModel(), priced));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Customer)]
    public async Task<IActionResult> Catering(CateringCheckoutViewModel input)
    {
        var priced = await orders.PriceCartAsync(cart.GetLines());
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();

        //Code and preferred status come from the account, never from the form
        var order = new CateringOrder
        {
            CustomerId = user.Id,
            CustomerCode = user.CustomerCode ?? string.Empty,
            PreferredCustomer = user.PreferredCustomer,
            DeliveryFee = DeliveryZones.Fee(input.Zone),
            EventDate = input.EventDate ?? default,
            GuestCount = input.GuestCount ?? 0,
            DeliveryAddress = input.DeliveryAddress?.Trim() ?? string.Empty,
            Today = AustinTime.Today(clock)
        };

        return await Place(order, priced, async () => View(await CateringModel(input, priced)));
    }

    //Shared flow: check the form and the order's own rules, price it, save it, show the receipt
    private async Task<IActionResult> Place(Order order, PricedCart priced, Func<Task<IActionResult>> showForm)
    {
        foreach (var problem in priced.Problems)
        {
            ModelState.AddModelError(string.Empty, problem);
        }

        //Run the model's data annotations (and IValidatableObject) against the built order
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(order, new(order), results, validateAllProperties: true);
        foreach (var result in results)
        {
            //Errors on fields the form shows go next to that field; the rest go in the summary
            var member = result.MemberNames.FirstOrDefault() ?? string.Empty;
            var key = FormFields.Contains(member) ? member : string.Empty;
            if (ModelState[key]?.Errors.Count > 0 && key != string.Empty) continue;
            ModelState.AddModelError(key, result.ErrorMessage ?? "Invalid value.");
        }

        if (!ModelState.IsValid)
        {
            return await showForm();
        }

        try
        {
            await orders.PlaceAsync(order, priced.Lines);
        }
        catch (EmptyOrderException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await showForm();
        }

        cart.Clear();
        RememberGuestOrder(order.OrderId);
        TempData["Message"] = "Order placed! The kitchen has it.";
        return RedirectToAction("Details", "Orders", new { id = order.OrderId });
    }

    private Task<IActionResult> Place(Order order, PricedCart priced, Func<IActionResult> showForm) =>
        Place(order, priced, () => Task.FromResult(showForm()));

    private async Task<CateringCheckoutViewModel> CateringModel(CateringCheckoutViewModel model, PricedCart priced)
    {
        var user = await users.GetUserAsync(User);
        model.Priced = priced;
        model.CustomerCode = user?.CustomerCode ?? string.Empty;
        model.PreferredCustomer = user?.PreferredCustomer ?? false;
        model.MinDate = AustinTime.Today(clock).AddDays(CateringOrder.LeadDays);
        return model;
    }

    private void RememberGuestOrder(int orderId)
    {
        var ids = HttpContext.Session.GetString(GuestOrdersKey);
        HttpContext.Session.SetString(GuestOrdersKey, string.IsNullOrEmpty(ids) ? $"{orderId}" : $"{ids},{orderId}");
    }
}
