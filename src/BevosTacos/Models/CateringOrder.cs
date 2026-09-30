using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTacos.Models;

//A catering order; no sales tax, but may be charged a delivery fee
public class CateringOrder : Order, IValidatableObject
{
    //Orders with a subtotal at or above this amount get free delivery
    public const decimal FreeDeliveryThreshold = 1000m;

    //Catering needs this many days' notice
    public const int LeadDays = 2;

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

    //The fee before any waiver, so the receipt can show what was saved
    public decimal RequestedDeliveryFee { get; private set; }

    //Copied from the customer's account when the order is placed; customers can't set it themselves
    [Display(Name = "Preferred Customer")]
    public bool PreferredCustomer { get; set; }

    [Display(Name = "Event Date")]
    public DateOnly EventDate { get; set; }

    [Display(Name = "Guests")]
    [Range(1, 5000, ErrorMessage = "Enter how many guests you are expecting.")]
    public int GuestCount { get; set; }

    [Display(Name = "Delivery Address")]
    [Required(ErrorMessage = "Delivery address is required.")]
    [StringLength(200)]
    public string DeliveryAddress { get; set; } = string.Empty;

    public string? DeliveryWaivedReason { get; private set; }

    //Today's date in Austin, set by whoever validates the order (lets tests pin the date)
    [NotMapped]
    public DateOnly Today { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EventDate < Today.AddDays(LeadDays))
        {
            yield return new ValidationResult($"Catering needs at least {LeadDays} days' notice.", [nameof(EventDate)]);
        }
    }

    protected override decimal CalcTotal()
    {
        RequestedDeliveryFee = DeliveryFee;
        DeliveryWaivedReason = null;

        //Preferred customers and big orders always get free delivery
        if (PreferredCustomer)
        {
            DeliveryWaivedReason = "Preferred customer";
        }
        else if (Subtotal >= FreeDeliveryThreshold)
        {
            DeliveryWaivedReason = "Order of $1,000 or more";
        }

        if (DeliveryWaivedReason is not null)
        {
            DeliveryFee = 0;
        }

        return Subtotal + DeliveryFee;
    }

    //How many tacos to plan for a party: 3 per guest, rounded up to a full dozen
    public static int SuggestedTacos(int guestCount) =>
        guestCount <= 0 ? 0 : (int)Math.Ceiling(guestCount * 3 / 12.0) * 12;
}
