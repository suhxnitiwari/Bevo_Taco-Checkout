using System.ComponentModel.DataAnnotations;

namespace BevosTacos.Models;

//A catering order; no sales tax, but may be charged a delivery fee
public class CateringOrder : Order
{
    //Orders with a subtotal at or above this amount get free delivery
    public const decimal FreeDeliveryThreshold = 1000m;

    public override CustomerType CustomerType => CustomerType.Catering;

    [Display(Name = "Customer Code")]
    [Required(ErrorMessage = "Customer code is required.")]
    [StringLength(4, MinimumLength = 2, ErrorMessage = "Customer code must be between 2 and 4 characters.")]
    [RegularExpression("^[a-zA-Z]+$", ErrorMessage = "Customer code must contain letters only.")]
    public string CustomerCode { get; set; } = string.Empty;

    //Uses the decimal overload so values like 250.01 aren't rounded to an int and slip through
    [Display(Name = "Delivery Fee")]
    [Range(typeof(decimal), "0", "250", ErrorMessage = "Delivery fee must be between $0 and $250.")]
    public decimal DeliveryFee { get; set; }

    //Preferred customers always get free delivery
    [Display(Name = "Preferred Customer")]
    public bool PreferredCustomer { get; set; }

    protected override decimal CalcTotal()
    {
        if (PreferredCustomer || Subtotal >= FreeDeliveryThreshold)
        {
            DeliveryFee = 0;
        }

        return Subtotal + DeliveryFee;
    }
}
