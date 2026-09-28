using System.ComponentModel.DataAnnotations;

namespace Tiwari_Suhani_HW2.Models;

//Represents an order placed by a walkup (in-person) customer
//Walkup customers are charged sales tax; there are no rules on the customer's name
public class WalkupOrder : Order
{
    //----- Named Constants (Fields) -----

    //Sales tax rate charged to all walkup customers (8.25%)
    public const decimal SALES_TAX_RATE = 0.0825m;

    //----- Properties -----

    //Name of the walkup customer - no validation rules, any value (even blank) is acceptable
    [Display(Name = "Customer Name")]
    public string? CustomerName { get; set; }

    //Sales tax charged on this order (Subtotal * SALES_TAX_RATE)
    [Display(Name = "Sales Tax")]
    public decimal SalesTax { get; set; }

    //----- Methods -----

    //Calls CalcSubtotals on the base Order class, then calculates SalesTax and Total
    public void CalcTotals()
    {
        try
        {
            //calculate subtotal
            base.CalcSubtotals();
        }
        catch (Exception ex)
        {
            throw new Exception("There was an error with the subtotals!", ex);
        }

        SalesTax = Subtotal * SALES_TAX_RATE;
        Total = Subtotal + SalesTax;
    }
}
