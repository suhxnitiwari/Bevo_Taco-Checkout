using System.Net;
using BevosTacos.Models;
using Microsoft.EntityFrameworkCore;

namespace BevosTacos.Tests.Integration;

public class AccessTests(BevoAppFactory app) : IClassFixture<BevoAppFactory>
{
    [Theory]
    [InlineData("/Kitchen")]
    [InlineData("/Manager")]
    [InlineData("/Manager/Menu")]
    [InlineData("/Manager/Customers")]
    [InlineData("/Manager/Export")]
    [InlineData("/Orders")]
    public async Task Guests_AreSentToSignIn(string url)
    {
        var response = await app.NewClient().Get(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData(Roles.Customer, "/Kitchen", false)]
    [InlineData(Roles.Customer, "/Manager", false)]
    [InlineData(Roles.Kitchen, "/Kitchen", true)]
    [InlineData(Roles.Kitchen, "/Manager", false)]
    [InlineData(Roles.Manager, "/Kitchen", true)]
    [InlineData(Roles.Manager, "/Manager", true)]
    [InlineData(Roles.Manager, "/Manager/Customers", true)]
    public async Task EachRole_SeesOnlyItsPages(string role, string url, bool allowed)
    {
        var client = app.NewClient();
        await client.SignInAs(role);

        var response = await client.Get(url);

        if (allowed)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString());
        }
    }

    [Fact]
    public async Task Customers_CantSeeEachOthersOrders()
    {
        var maya = app.NewClient();
        await maya.SignInAs(Roles.Customer);
        var itemId = app.WithDb(db => db.MenuItems.First().MenuItemId);
        await maya.Post("/Menu/Add", ("menuItemId", itemId.ToString()), ("quantity", "1"));
        var placed = await maya.Post("/Checkout/Walkup", ("TipPercent", "0"));
        var url = placed.Headers.Location!.ToString();

        var other = app.NewClient();
        await other.SignInAs("Preferred");

        Assert.Equal(HttpStatusCode.NotFound, (await other.Get(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Post(url.Replace("Details", "Cancel"))).StatusCode);

        //Staff can open any order
        var kitchen = app.NewClient();
        await kitchen.SignInAs(Roles.Kitchen);
        Assert.Equal(HttpStatusCode.OK, (await kitchen.Get(url)).StatusCode);
    }

    [Fact]
    public async Task Kitchen_AdvancesOrders_AndCustomersCanOnlyCancelBeforeCooking()
    {
        var guest = app.NewClient();
        var itemId = app.WithDb(db => db.MenuItems.First().MenuItemId);
        await guest.Post("/Menu/Add", ("menuItemId", itemId.ToString()), ("quantity", "1"));
        var url = (await guest.Post("/Checkout/Walkup", ("TipPercent", "0"))).Headers.Location!.ToString();
        var id = int.Parse(url.Split('/').Last());

        var kitchen = app.NewClient();
        await kitchen.SignInAs(Roles.Kitchen);
        await kitchen.Post($"/Kitchen/Advance/{id}", tokenPage: "/Kitchen", fields: new());

        await guest.Post($"/Orders/Cancel/{id}");

        var status = app.WithDb(db => db.Orders.AsNoTracking().Single(o => o.OrderId == id).Status);
        Assert.Equal(OrderStatus.Cooking, status);
    }

    [Fact]
    public async Task Posts_WithoutAnAntiforgeryToken_AreRejected()
    {
        var client = app.NewClient();

        var response = await client.Http.PostAsync("/Account/Demo", new FormUrlEncodedContent([new("role", Roles.Manager)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_RejectsATakenCustomerCode()
    {
        var client = app.NewClient();

        var response = await client.Post("/Account/Register", new Dictionary<string, string>
        {
            ["FirstName"] = "Copycat", ["Email"] = "copycat@example.test", ["CustomerCode"] = "misa",
            ["Password"] = "LongEnough1", ["ConfirmPassword"] = "LongEnough1"
        }, tokenPage: "/Account/Register");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("That customer code is taken.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_CreatesACustomerWhoCanOrderCatering()
    {
        var client = app.NewClient();
        var code = "Q" + (char)('A' + Random.Shared.Next(26)) + (char)('A' + Random.Shared.Next(26));

        var response = await client.Post("/Account/Register", new Dictionary<string, string>
        {
            ["FirstName"] = "New", ["Email"] = $"new-{Guid.NewGuid():N}@example.test", ["CustomerCode"] = code.ToLowerInvariant(),
            ["Password"] = "LongEnough1", ["ConfirmPassword"] = "LongEnough1"
        }, tokenPage: "/Account/Register");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.Get("/Checkout/Catering")).StatusCode); // empty cart -> back to cart
        Assert.Equal("/Cart", (await client.Get("/Checkout/Catering")).Headers.Location!.ToString());
        Assert.True(app.WithDb(db => db.Users.Any(u => u.CustomerCode == code)));
    }
}
