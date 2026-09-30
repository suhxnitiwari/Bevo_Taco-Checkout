using System.ComponentModel.DataAnnotations;
using BevosTacos.Models;
using static BevosTacos.Tests.OrderLines;

namespace BevosTacos.Tests;

public class CateringOrderTests
{
    private static readonly DateOnly Today = new(2026, 9, 30);

    //A catering order that passes every rule, so each test can break just one
    private static CateringOrder ValidOrder(params OrderLine[] lines) => new()
    {
        CustomerCode = "MISA",
        DeliveryFee = 100m,
        GuestCount = 40,
        DeliveryAddress = "2110 Speedway",
        EventDate = Today.AddDays(5),
        Today = Today,
        Lines = lines.Length > 0 ? lines.ToList() : Of(Tacos(1))
    };

    [Fact]
    public void RegularCustomer_PaysDeliveryFee()
    {
        var order = ValidOrder(Tacos(30), Burgers(30));

        order.CalcTotals();

        Assert.Equal(60, order.TotalItems);
        Assert.Equal(217.50m, order.Subtotal);
        Assert.Equal(100m, order.DeliveryFee);
        Assert.Equal(317.50m, order.Total);
        Assert.Null(order.DeliveryWaivedReason);
    }

    [Fact]
    public void PreferredCustomer_GetsFreeDelivery()
    {
        var order = ValidOrder(Tacos(30), Burgers(30));
        order.PreferredCustomer = true;

        order.CalcTotals();

        Assert.Equal(0m, order.DeliveryFee);
        Assert.Equal(100m, order.RequestedDeliveryFee);
        Assert.Equal(217.50m, order.Total);
        Assert.Equal("Preferred customer", order.DeliveryWaivedReason);
    }

    [Fact]
    public void LargeOrder_GetsFreeDelivery()
    {
        var order = ValidOrder(Tacos(500), Burgers(500));

        order.CalcTotals();

        Assert.Equal(3625m, order.Subtotal);
        Assert.Equal(0m, order.DeliveryFee);
        Assert.Equal(3625m, order.Total);
        Assert.Equal("Order of $1,000 or more", order.DeliveryWaivedReason);
    }

    [Theory]
    [InlineData(199, 100, 997.25, 100)]  // just under the threshold: fee charged
    [InlineData(200, 100, 1000.00, 0)]   // exactly at the threshold: free
    public void FreeDeliveryThreshold_IsInclusive(int tacos, int burgers, decimal expectedSubtotal, decimal expectedFee)
    {
        var order = ValidOrder(Tacos(tacos), Burgers(burgers));

        order.CalcTotals();

        Assert.Equal(expectedSubtotal, order.Subtotal);
        Assert.Equal(expectedFee, order.DeliveryFee);
    }

    [Fact]
    public void RecalculatingAfterAWaiver_StillKnowsTheRequestedFee()
    {
        var order = ValidOrder(Tacos(10));
        order.PreferredCustomer = true;
        order.CalcTotals();

        order.PreferredCustomer = false;
        order.DeliveryFee = order.RequestedDeliveryFee;
        order.CalcTotals();

        Assert.Equal(100m, order.DeliveryFee);
    }

    [Fact]
    public void EmptyOrder_Throws()
    {
        var order = ValidOrder();
        order.Lines.Clear();

        Assert.Throws<EmptyOrderException>(order.CalcTotals);
    }

    [Fact]
    public void CustomerType_IsCatering()
    {
        Assert.Equal(CustomerType.Catering, new CateringOrder().CustomerType);
    }

    [Theory]
    [InlineData("AB", true)]
    [InlineData("MISA", true)]
    [InlineData("A", false)]      // too short
    [InlineData("ABCDE", false)]  // too long
    [InlineData("AB1", false)]    // digits not allowed
    [InlineData("A-B", false)]    // symbols not allowed
    [InlineData("", false)]       // required
    public void CustomerCode_MustBeTwoToFourLetters(string code, bool isValid)
    {
        var order = ValidOrder();
        order.CustomerCode = code;

        Assert.Equal(isValid, IsValid(order));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(250, true)]
    [InlineData(-0.01, false)]
    [InlineData(250.01, false)]
    public void DeliveryFee_MustBeBetweenZeroAnd250(decimal fee, bool isValid)
    {
        var order = ValidOrder();
        order.DeliveryFee = fee;

        Assert.Equal(isValid, IsValid(order));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]   // exactly two days' notice is enough
    [InlineData(30, true)]
    public void EventDate_NeedsTwoDaysNotice(int daysAhead, bool isValid)
    {
        var order = ValidOrder();
        order.EventDate = Today.AddDays(daysAhead);

        Assert.Equal(isValid, IsValid(order));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5001, false)]
    public void GuestCount_MustBeReasonable(int guests, bool isValid)
    {
        var order = ValidOrder();
        order.GuestCount = guests;

        Assert.Equal(isValid, IsValid(order));
    }

    [Fact]
    public void DeliveryAddress_IsRequired()
    {
        var order = ValidOrder();
        order.DeliveryAddress = "";

        Assert.False(IsValid(order));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 12)]
    [InlineData(10, 36)]  // 30 tacos rounds up to 3 dozen
    [InlineData(40, 120)]
    public void SuggestedTacos_IsThreePerGuestInWholeDozens(int guests, int expected)
    {
        Assert.Equal(expected, CateringOrder.SuggestedTacos(guests));
    }

    private static bool IsValid(object model) =>
        Validator.TryValidateObject(model, new ValidationContext(model), null, validateAllProperties: true);
}
