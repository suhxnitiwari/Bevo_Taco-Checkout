using System.Net;
using BevosTacos.Data;
using BevosTacos.Models;
using Microsoft.EntityFrameworkCore;

namespace BevosTacos.Tests.Integration;

public class OrderingTests(BevoAppFactory app) : IClassFixture<BevoAppFactory>
{
    private int ItemId(string name) => app.WithDb(db => db.MenuItems.Single(m => m.Name == name).MenuItemId);
    private int AddOnId(string name) => app.WithDb(db => db.AddOns.Single(a => a.Name == name).AddOnId);
    private string EventDate => DateOnly.FromDateTime(DateTime.Today.AddDays(7)).ToString("yyyy-MM-dd");

    private static int OrderIdFrom(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith("/Orders/Details/", location);
        return int.Parse(location.Split('/').Last());
    }

    private Order LoadOrder(int id) => app.WithDb(db => db.Orders.Include(o => o.Lines).AsNoTracking().Single(o => o.OrderId == id));

    [Fact]
    public async Task Guest_CanPlaceAWalkupOrder_AndTheServerPricesIt()
    {
        var client = app.NewClient();
        await client.Post("/Menu/Add", ("menuItemId", ItemId("Classic Longhorn").ToString()), ("addOnIds", AddOnId("Add queso").ToString()), ("quantity", "2"));
        await client.Post("/Menu/Add", ("menuItemId", ItemId("Topo Chico").ToString()), ("quantity", "1"));

        var response = await client.Post("/Checkout/Walkup", ("CustomerName", "Suhani"), ("TipPercent", "18"));

        var order = Assert.IsType<WalkupOrder>(LoadOrder(OrderIdFrom(response)));
        Assert.Equal(12.00m, order.Subtotal);   // 2 x ($4.50 + $0.50) + $2.00
        Assert.Equal(0.99m, order.SalesTax);
        Assert.Equal(2.16m, order.Tip);
        Assert.Equal(15.15m, order.Total);
        Assert.Equal(OrderStatus.New, order.Status);
        Assert.Null(order.CustomerId);

        //The guest can see their own receipt, and the cart is empty again
        Assert.Equal(HttpStatusCode.OK, (await client.Get($"/Orders/Details/{order.OrderId}")).StatusCode);
        Assert.Contains("Your cart is empty", await (await client.Get("/Cart")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EmptyCart_CantCheckOut()
    {
        var client = app.NewClient();

        var response = await client.Post("/Checkout/Walkup", ("TipPercent", "0"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Order must contain at least one item.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AddOnsThatDontBelongToAnItem_AreNotCharged()
    {
        var client = app.NewClient();
        //A double patty on a taco isn't allowed, so a tampered form shouldn't get one
        await client.Post("/Menu/Add", ("menuItemId", ItemId("Al Pastor").ToString()), ("addOnIds", AddOnId("Double patty").ToString()), ("quantity", "1"));

        var response = await client.Post("/Checkout/Walkup", ("TipPercent", "0"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Some add-ons aren&#x27;t available on Al Pastor.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GuestCantSeeSomeoneElsesReceipt()
    {
        var first = app.NewClient();
        await first.Post("/Menu/Add", ("menuItemId", ItemId("Migas").ToString()), ("quantity", "1"));
        var id = OrderIdFrom(await first.Post("/Checkout/Walkup", ("TipPercent", "0")));

        var stranger = app.NewClient();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Get($"/Orders/Details/{id}")).StatusCode);
    }

    [Fact]
    public async Task Catering_RequiresASignedInCustomer()
    {
        var guest = app.NewClient();
        var response = await guest.Get("/Checkout/Catering");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("http://localhost/Account/Login", response.Headers.Location!.ToString());

        var kitchen = app.NewClient();
        await kitchen.SignInAs(Roles.Kitchen);
        response = await kitchen.Get("/Checkout/Catering");
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Catering_ChargesTheZoneFee_AndIgnoresAFakePreferredFlag()
    {
        var client = app.NewClient();
        await client.SignInAs(Roles.Customer);
        await client.Post("/Menu/Add", ("menuItemId", ItemId("Smoked Brisket").ToString()), ("quantity", "120"));

        var response = await client.Post("/Checkout/Catering", new Dictionary<string, string>
        {
            ["EventDate"] = EventDate,
            ["GuestCount"] = "40",
            ["DeliveryAddress"] = "2110 Speedway",
            ["Zone"] = nameof(DeliveryZone.Greater),
            ["PreferredCustomer"] = "true",   // not a real form field; must be ignored
            ["DeliveryFee"] = "0"             // same
        });

        var order = Assert.IsType<CateringOrder>(LoadOrder(OrderIdFrom(response)));
        Assert.Equal("MISA", order.CustomerCode);
        Assert.False(order.PreferredCustomer);
        Assert.Equal(330m, order.Subtotal);
        Assert.Equal(175m, order.DeliveryFee);
        Assert.Equal(505m, order.Total);
    }

    [Fact]
    public async Task Catering_PreferredCustomerGetsFreeDelivery()
    {
        var client = app.NewClient();
        await client.SignInAs("Preferred");
        await client.Post("/Menu/Add", ("menuItemId", ItemId("Al Pastor").ToString()), ("quantity", "24"));

        var response = await client.Post("/Checkout/Catering", new Dictionary<string, string>
        {
            ["EventDate"] = EventDate, ["GuestCount"] = "8", ["DeliveryAddress"] = "UT Tower", ["Zone"] = nameof(DeliveryZone.Outside)
        });

        var order = Assert.IsType<CateringOrder>(LoadOrder(OrderIdFrom(response)));
        Assert.Equal(0m, order.DeliveryFee);
        Assert.Equal(250m, order.RequestedDeliveryFee);
        Assert.Equal("Preferred customer", order.DeliveryWaivedReason);
    }

    [Fact]
    public async Task Catering_TooSoonShowsAnError()
    {
        var client = app.NewClient();
        await client.SignInAs(Roles.Customer);
        await client.Post("/Menu/Add", ("menuItemId", ItemId("Migas").ToString()), ("quantity", "12"));

        var response = await client.Post("/Checkout/Catering", new Dictionary<string, string>
        {
            ["EventDate"] = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"), ["GuestCount"] = "4", ["DeliveryAddress"] = "UT Tower", ["Zone"] = nameof(DeliveryZone.Campus)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Catering needs at least 2 days&#x27; notice.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SoldOutItems_CantBeAdded()
    {
        var manager = app.NewClient();
        await manager.SignInAs(Roles.Manager);
        var id = ItemId("Street Elote");
        await manager.Post($"/Manager/UpdateItem/{id}", ("price", "3.50"), ("isAvailable", "false"));

        var guest = app.NewClient();
        await guest.Post("/Menu/Add", ("menuItemId", id.ToString()), ("quantity", "1"));

        Assert.Contains("Your cart is empty", await (await guest.Get("/Cart")).Content.ReadAsStringAsync());
        await manager.Post($"/Manager/UpdateItem/{id}", ("price", "3.50"), ("isAvailable", "true"));
    }

    [Fact]
    public async Task PriceChanges_DontRewriteOldReceipts()
    {
        var guest = app.NewClient();
        var id = ItemId("Chips & Guac");
        await guest.Post("/Menu/Add", ("menuItemId", id.ToString()), ("quantity", "1"));
        var orderId = OrderIdFrom(await guest.Post("/Checkout/Walkup", ("TipPercent", "0")));

        var manager = app.NewClient();
        await manager.SignInAs(Roles.Manager);
        await manager.Post($"/Manager/UpdateItem/{id}", ("price", "5.00"), ("isAvailable", "true"));

        Assert.Equal(4.00m, LoadOrder(orderId).Lines.Single().UnitPrice);
        Assert.Equal(5.00m, app.WithDb(db => db.MenuItems.Single(m => m.MenuItemId == id).Price));
        await manager.Post($"/Manager/UpdateItem/{id}", ("price", "4.00"), ("isAvailable", "true"));
    }
}
