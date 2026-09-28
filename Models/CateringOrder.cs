using System.ComponentModel.DataAnnotations;

namespace Tiwari_Suhani_HW2.Models;

//Represents an order placed by a catering customer
//Catering customers are not charged tax, but may be charged a delivery fee
public class CateringOrder : Order
{
    //----- Properties -----

    //Customer code for this catering order - must be 2 to 4 letters, no numbers or special characters
    [Display(Name = "Customer Code")]
    [Required(ErrorMessage = "Customer code is required.")]
    [StringLength(4, MinimumLength = 2, ErrorMessage = "Customer code must be between 2 and 4 characters.")]
    [RegularExpression("^[a-zA-Z]+$", ErrorMessage = "Customer code must contain letters only.")]
    public string CustomerCode { get; set; } = string.Empty;

    //Delivery fee for this order - must be between $0 and $250 (inclusive)
    [Display(Name = "Delivery Fee")]
    [Range(0, 250, ErrorMessage = "Delivery fee must be between $0 and $250.")]
    public decimal DeliveryFee { get; set; }

    //True if this customer is a preferred customer (preferred customers get free delivery)
    [Display(Name = "Preferred Customer")]
    public bool PreferredCustomer { get; set; }

    //----- Methods -----

    //Calls CalcSubtotals on the base Order class, sets DeliveryFee, and calculates Total
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

        //Preferred customers, and any order with a subtotal of $1,000 or more, get free delivery
        if (PreferredCustomer || Subtotal >= 1000)
        {
            DeliveryFee = 0;
        }

        Total = Subtotal + DeliveryFee;
    }
}
