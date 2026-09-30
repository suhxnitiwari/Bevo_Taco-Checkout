using System.ComponentModel.DataAnnotations;

namespace BevosTacos.Models;

//An in-person order; walkup customers pay sales tax and can leave a tip
public class WalkupOrder : Order
{
    //Sales tax rate charged to all walkup customers (8.25%)
    public const decimal SalesTaxRate = 0.0825m;

    //Tip choices offered at checkout, as a percent of the subtotal
    public static readonly int[] TipOptions = [0, 15, 18, 20];

    public override CustomerType CustomerType => CustomerType.Walkup;

    //Optional - any value, including blank, is accepted
    [Display(Name = "Customer Name")]
    [StringLength(40, ErrorMessage = "Name must be 40 characters or fewer.")]
    public string? CustomerName { get; set; }

    [Display(Name = "Tip")]
    [AllowedValues(0, 15, 18, 20, ErrorMessage = "Choose one of the tip options.")]
    public int TipPercent { get; set; }

    [Display(Name = "Sales Tax")]
    public decimal SalesTax { get; private set; }

    [Display(Name = "Tip")]
    public decimal Tip { get; private set; }

    protected override decimal CalcTotal()
    {
        //Round to the nearest cent so the stored amounts match what the customer is charged
        SalesTax = Math.Round(Subtotal * SalesTaxRate, 2, MidpointRounding.AwayFromZero);
        Tip = Math.Round(Subtotal * TipPercent / 100m, 2, MidpointRounding.AwayFromZero);
        return Subtotal + SalesTax + Tip;
    }
}
