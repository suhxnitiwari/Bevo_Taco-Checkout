using System.ComponentModel.DataAnnotations;
using BevosTacos.Models;
using static BevosTacos.Tests.OrderLines;

namespace BevosTacos.Tests;

public class WalkupOrderTests
{
    [Fact]
    public void OneTacoOneBurger_CalculatesSubtotalTaxAndTotal()
    {
        var order = new WalkupOrder { Lines = Of(Tacos(1), Burgers(1)) };

        order.CalcTotals();

        Assert.Equal(2, order.TotalItems);
        Assert.Equal(7.25m, order.Subtotal);
        Assert.Equal(0.60m, order.SalesTax);
        Assert.Equal(7.85m, order.Total);
    }

    [Fact]
    public void SalesTax_IsRoundedToTheCent()
    {
        var order = new WalkupOrder { Lines = Of(Tacos(3)) };

        order.CalcTotals();

        // 8.25 * 0.0825 = 0.680625
        Assert.Equal(0.68m, order.SalesTax);
        Assert.Equal(8.93m, order.Total);
    }

    [Fact]
    public void SalesTax_RoundsHalfAwayFromZero()
    {
        // $2.00 * 8.25% = 0.165 -> 0.17 (banker's rounding would give 0.16)
        var order = new WalkupOrder { Lines = Of(Drinks(1)) };

        order.CalcTotals();

        Assert.Equal(0.17m, order.SalesTax);
    }

    [Fact]
    public void AddOns_ArePartOfTheUnitPrice()
    {
        // Burger with a double patty ($2.00) and guac ($0.75)
        var order = new WalkupOrder { Lines = Of(Burgers(2, addOns: 2.75m)) };

        order.CalcTotals();

        Assert.Equal(14.50m, order.Subtotal);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(15, 1.35)]
    [InlineData(18, 1.62)]
    [InlineData(20, 1.80)]
    public void Tip_IsAPercentOfTheSubtotal(int percent, decimal expectedTip)
    {
        var order = new WalkupOrder { TipPercent = percent, Lines = Of(Burgers(2)) };

        order.CalcTotals();

        Assert.Equal(expectedTip, order.Tip);
        Assert.Equal(9.00m + 0.74m + expectedTip, order.Total);
    }

    [Fact]
    public void OnlyOfferedTips_AreValid()
    {
        Assert.Contains(Validate(new WalkupOrder { TipPercent = 50 }), e => e.ErrorMessage == "Choose one of the tip options.");
        Assert.Empty(Validate(new WalkupOrder { TipPercent = 18 }));
    }

    [Fact]
    public void EmptyOrder_Throws()
    {
        var order = new WalkupOrder { Lines = Of(Tacos(0)) };

        var ex = Assert.Throws<EmptyOrderException>(order.CalcTotals);

        Assert.Equal("Order must contain at least one item.", ex.Message);
    }

    [Fact]
    public void CustomerType_IsWalkup()
    {
        Assert.Equal(CustomerType.Walkup, new WalkupOrder().CustomerType);
    }

    [Fact]
    public void BlankCustomerName_IsAllowed()
    {
        Assert.Empty(Validate(new WalkupOrder { CustomerName = null }));
    }

    [Fact]
    public void LongCustomerName_FailsValidation()
    {
        Assert.NotEmpty(Validate(new WalkupOrder { CustomerName = new string('a', 41) }));
    }

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
