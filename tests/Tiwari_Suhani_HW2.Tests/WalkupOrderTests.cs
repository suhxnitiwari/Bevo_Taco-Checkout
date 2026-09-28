using System.ComponentModel.DataAnnotations;
using Tiwari_Suhani_HW2.Models;

namespace Tiwari_Suhani_HW2.Tests;

public class WalkupOrderTests
{
    [Fact]
    public void OneTacoOneBurger_CalculatesSubtotalTaxAndTotal()
    {
        var order = new WalkupOrder { NumberOfTacos = 1, NumberOfBurgers = 1 };

        order.CalcTotals();

        Assert.Equal(2, order.TotalItems);
        Assert.Equal(2.75m, order.TacoSubtotal);
        Assert.Equal(4.50m, order.BurgerSubtotal);
        Assert.Equal(7.25m, order.Subtotal);
        Assert.Equal(0.60m, order.SalesTax);
        Assert.Equal(7.85m, order.Total);
    }

    [Fact]
    public void SalesTax_IsRoundedToTheCent()
    {
        var order = new WalkupOrder { NumberOfTacos = 3 };

        order.CalcTotals();

        // 8.25 * 0.0825 = 0.680625
        Assert.Equal(0.68m, order.SalesTax);
        Assert.Equal(8.93m, order.Total);
    }

    [Fact]
    public void EmptyOrder_Throws()
    {
        var order = new WalkupOrder();

        var ex = Assert.Throws<Exception>(order.CalcTotals);

        Assert.Equal("Order must contain at least one taco or burger.", ex.InnerException?.Message);
    }

    [Fact]
    public void NegativeBurgers_FailsValidation()
    {
        var order = new WalkupOrder { NumberOfTacos = 1, NumberOfBurgers = -1 };

        var errors = Validate(order);

        Assert.Contains(errors, e => e.ErrorMessage == "Number of burgers cannot be negative.");
    }

    [Fact]
    public void BlankCustomerName_IsAllowed()
    {
        var order = new WalkupOrder { CustomerName = null, NumberOfTacos = 1 };

        Assert.Empty(Validate(order));
    }

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
