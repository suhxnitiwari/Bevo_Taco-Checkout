using System.ComponentModel.DataAnnotations;
using BevosTacos.Models;

namespace BevosTacos.Tests;

public class CateringOrderTests
{
    [Fact]
    public void RegularCustomer_PaysDeliveryFee()
    {
        var order = new CateringOrder
        {
            CustomerCode = "MISA",
            NumberOfTacos = 30,
            NumberOfBurgers = 30,
            DeliveryFee = 100m,
            PreferredCustomer = false
        };

        order.CalcTotals();

        Assert.Equal(60, order.TotalItems);
        Assert.Equal(217.50m, order.Subtotal);
        Assert.Equal(100m, order.DeliveryFee);
        Assert.Equal(317.50m, order.Total);
    }

    [Fact]
    public void PreferredCustomer_GetsFreeDelivery()
    {
        var order = new CateringOrder
        {
            CustomerCode = "MISA",
            NumberOfTacos = 30,
            NumberOfBurgers = 30,
            DeliveryFee = 100m,
            PreferredCustomer = true
        };

        order.CalcTotals();

        Assert.Equal(0m, order.DeliveryFee);
        Assert.Equal(217.50m, order.Total);
    }

    [Fact]
    public void LargeOrder_GetsFreeDelivery()
    {
        var order = new CateringOrder
        {
            CustomerCode = "MMMM",
            NumberOfTacos = 500,
            NumberOfBurgers = 500,
            DeliveryFee = 100m,
            PreferredCustomer = false
        };

        order.CalcTotals();

        Assert.Equal(3625m, order.Subtotal);
        Assert.Equal(0m, order.DeliveryFee);
        Assert.Equal(3625m, order.Total);
    }

    [Theory]
    [InlineData(199, 100, 997.25, 100)]  // just under the threshold: fee charged
    [InlineData(200, 100, 1000.00, 0)]   // exactly at the threshold: free
    public void FreeDeliveryThreshold_IsInclusive(int tacos, int burgers, decimal expectedSubtotal, decimal expectedFee)
    {
        var order = new CateringOrder { CustomerCode = "AB", NumberOfTacos = tacos, NumberOfBurgers = burgers, DeliveryFee = 100m };

        order.CalcTotals();

        Assert.Equal(expectedSubtotal, order.Subtotal);
        Assert.Equal(expectedFee, order.DeliveryFee);
    }

    [Fact]
    public void EmptyOrder_Throws()
    {
        var order = new CateringOrder { CustomerCode = "AB" };

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
        var order = new CateringOrder { CustomerCode = code, NumberOfTacos = 1 };

        Assert.Equal(isValid, IsValid(order));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(250, true)]
    [InlineData(-0.01, false)]
    [InlineData(250.01, false)]
    public void DeliveryFee_MustBeBetweenZeroAnd250(decimal fee, bool isValid)
    {
        var order = new CateringOrder { CustomerCode = "AB", NumberOfTacos = 1, DeliveryFee = fee };

        Assert.Equal(isValid, IsValid(order));
    }

    private static bool IsValid(object model) =>
        Validator.TryValidateObject(model, new ValidationContext(model), null, validateAllProperties: true);
}
