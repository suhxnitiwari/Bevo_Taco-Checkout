using System.ComponentModel.DataAnnotations;

namespace BevosTacos.Models;

public enum CustomerType
{
    Walkup,
    Catering
}

//Shared pricing and validation for every kind of order
public abstract class Order
{
    //Price charged per taco
    public const decimal TacoPrice = 2.75m;

    //Price charged per burger
    public const decimal BurgerPrice = 4.50m;

    //Each subclass reports its own customer type
    [Display(Name = "Customer Type")]
    public abstract CustomerType CustomerType { get; }

    [Display(Name = "Number of Tacos")]
    [Range(0, int.MaxValue, ErrorMessage = "Number of tacos cannot be negative.")]
    public int NumberOfTacos { get; set; }

    [Display(Name = "Number of Burgers")]
    [Range(0, int.MaxValue, ErrorMessage = "Number of burgers cannot be negative.")]
    public int NumberOfBurgers { get; set; }

    [Display(Name = "Total Items")]
    public int TotalItems { get; private set; }

    [Display(Name = "Taco Subtotal")]
    public decimal TacoSubtotal { get; private set; }

    [Display(Name = "Burger Subtotal")]
    public decimal BurgerSubtotal { get; private set; }

    [Display(Name = "Subtotal")]
    public decimal Subtotal { get; private set; }

    //Walkup orders: Subtotal + SalesTax   |   Catering orders: Subtotal + DeliveryFee
    [Display(Name = "Total")]
    public decimal Total { get; protected set; }

    //Calculates the subtotals, then lets the subclass apply its own charges to get the Total
    public void CalcTotals()
    {
        CalcSubtotals();
        Total = CalcTotal();
    }

    //Returns the final total for this kind of order; Subtotal is already set when this runs
    protected abstract decimal CalcTotal();

    //Throws EmptyOrderException if the customer did not order any items
    private void CalcSubtotals()
    {
        TotalItems = NumberOfTacos + NumberOfBurgers;

        if (TotalItems == 0)
        {
            throw new EmptyOrderException();
        }

        TacoSubtotal = NumberOfTacos * TacoPrice;
        BurgerSubtotal = NumberOfBurgers * BurgerPrice;
        Subtotal = TacoSubtotal + BurgerSubtotal;
    }
}
