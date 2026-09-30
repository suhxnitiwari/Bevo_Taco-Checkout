using BevosTacos.Data;
using BevosTacos.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BevosTacos.Controllers;

public class AccountController(UserManager<AppUser> users, SignInManager<AppUser> signIn, IConfiguration config) : Controller
{
    private bool DemoEnabled => config.GetValue("Demo:Enabled", true);

    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["DemoEnabled"] = DemoEnabled;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        ViewData["DemoEnabled"] = DemoEnabled;
        if (!ModelState.IsValid) return View(model);

        var result = await signIn.PasswordSignInAsync(model.Email, model.Password, isPersistent: true, lockoutOnFailure: true);
        if (result.Succeeded) return LocalRedirect(SafeReturnUrl(model.ReturnUrl));

        ModelState.AddModelError(string.Empty, result.IsLockedOut ? "Too many attempts. Try again in a few minutes." : "Email or password is incorrect.");
        return View(model);
    }

    public IActionResult Register(string? returnUrl = null) => View(new RegisterViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        model.CustomerCode = model.CustomerCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (ModelState.IsValid && await users.Users.AnyAsync(u => u.CustomerCode == model.CustomerCode))
        {
            ModelState.AddModelError(nameof(model.CustomerCode), "That customer code is taken.");
        }
        if (!ModelState.IsValid) return View(model);

        var user = new AppUser { UserName = model.Email, Email = model.Email, FirstName = model.FirstName.Trim(), CustomerCode = model.CustomerCode };
        var result = await users.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await users.AddToRoleAsync(user, Roles.Customer);
        await signIn.SignInAsync(user, isPersistent: true);
        return LocalRedirect(SafeReturnUrl(model.ReturnUrl));
    }

    //One-click sign-in as a seeded demo account, so visitors can try every role
    [HttpPost]
    public async Task<IActionResult> Demo(string role, string? returnUrl = null)
    {
        if (!DemoEnabled) return NotFound();

        var email = role switch
        {
            Roles.Manager => SeedData.DemoManagerEmail,
            Roles.Kitchen => SeedData.DemoKitchenEmail,
            "Preferred" => SeedData.DemoPreferredEmail,
            _ => SeedData.DemoCustomerEmail
        };
        var user = await users.FindByEmailAsync(email);
        if (user is null) return NotFound();

        await signIn.SignInAsync(user, isPersistent: false);
        var landing = role switch
        {
            Roles.Manager => Url.Action("Index", "Manager"),
            Roles.Kitchen => Url.Action("Index", "Kitchen"),
            _ => Url.Action("Index", "Menu")
        };
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : landing!);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private string SafeReturnUrl(string? url) => Url.IsLocalUrl(url) ? url! : Url.Action("Index", "Menu")!;
}
