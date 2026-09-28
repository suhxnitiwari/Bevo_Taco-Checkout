using System.ComponentModel.DataAnnotations;

namespace BevosTacos.Models;

//An in-person order; walkup customers pay sales tax
public class WalkupOrder : Order
{
    //Sales tax rate charged to all walkup customers (8.25%)
    public const decimal SalesTaxRate = 0.0825m;

    public override CustomerType CustomerType => CustomerType.Walkup;

    //Optional - any value, including blank, is accepted
    [Display(Name = "Customer Name")]
    public string? CustomerName { get; set; }

    [Display(Name = "Sales Tax")]
    public decimal SalesTax { get; private set; }

    protected override decimal CalcTotal()
    {
        //Round tax to the nearest cent so the stored amount matches what the customer is charged
        SalesTax = Math.Round(Subtotal * SalesTaxRate, 2, MidpointRounding.AwayFromZero);
        return Subtotal + SalesTax;
    }
}
