using System.ComponentModel.DataAnnotations;
using BevosTacos.Services;

namespace BevosTacos.Models;

public class MenuViewModel
{
    public required List<MenuItem> Items { get; init; }
    public MenuCategory Category { get; init; }
}

public class CartViewModel
{
    public required List<CartLine> Cart { get; init; }
    public required PricedCart Priced { get; init; }
}

//Delivery zones set the catering delivery fee, which the model still checks is $0-$250
public enum DeliveryZone
{
    [Display(Name = "UT campus")] Campus,
    [Display(Name = "Central Austin")] Central,
    [Display(Name = "Greater Austin")] Greater,
    [Display(Name = "Outside Austin")] Outside
}

public static class DeliveryZones
{
    public static decimal Fee(DeliveryZone zone) => zone switch
    {
        DeliveryZone.Campus => 50m,
        DeliveryZone.Central => 100m,
        DeliveryZone.Greater => 175m,
        _ => 250m
    };
}

public class WalkupCheckoutViewModel
{
    [Display(Name = "Name for the order")]
    [StringLength(40, ErrorMessage = "Name must be 40 characters or fewer.")]
    public string? CustomerName { get; set; }

    [Display(Name = "Tip the crew")]
    public int TipPercent { get; set; }

    public PricedCart? Priced { get; set; }
}

public class CateringCheckoutViewModel
{
    [Display(Name = "Event date")]
    [Required(ErrorMessage = "Pick a date for your event.")]
    [DataType(DataType.Date)]
    public DateOnly? EventDate { get; set; }

    [Display(Name = "Guests")]
    [Required(ErrorMessage = "Enter how many guests you are expecting.")]
    [Range(1, 5000, ErrorMessage = "Enter how many guests you are expecting.")]
    public int? GuestCount { get; set; }

    [Display(Name = "Delivery zone")]
    public DeliveryZone Zone { get; set; } = DeliveryZone.Central;

    [Display(Name = "Delivery address")]
    [Required(ErrorMessage = "Delivery address is required.")]
    [StringLength(200)]
    public string DeliveryAddress { get; set; } = string.Empty;

    public PricedCart? Priced { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public bool PreferredCustomer { get; set; }
    public DateOnly MinDate { get; set; }
}

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Display(Name = "First name")]
    [Required, StringLength(40)]
    public string FirstName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Customer code")]
    [Required(ErrorMessage = "Customer code is required.")]
    [StringLength(4, MinimumLength = 2, ErrorMessage = "Customer code must be between 2 and 4 characters.")]
    [RegularExpression("^[a-zA-Z]+$", ErrorMessage = "Customer code must contain letters only.")]
    public string CustomerCode { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Confirm password")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords don't match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class KitchenViewModel
{
    public required List<Order> Orders { get; init; }
}

public record DailyRevenue(DateOnly Day, decimal Walkup, decimal Catering)
{
    public decimal Total => Walkup + Catering;
}

public record ItemSales(string Name, int Quantity, decimal Revenue);

public class DashboardViewModel
{
    public int Days { get; init; }
    public decimal Revenue { get; init; }
    public int OrderCount { get; init; }
    public decimal AverageTicket { get; init; }
    public int ItemsSold { get; init; }
    public decimal SalesTax { get; init; }
    public decimal Tips { get; init; }
    public int FreeDeliveries { get; init; }
    public decimal DeliveryFeesWaived { get; init; }
    public int Cancelled { get; init; }
    public decimal WalkupRevenue { get; init; }
    public decimal CateringRevenue { get; init; }
    public int WalkupOrders { get; init; }
    public int CateringOrders { get; init; }
    public required List<DailyRevenue> Daily { get; init; }
    public required List<ItemSales> TopItems { get; init; }
    public required Dictionary<MenuCategory, decimal> ByCategory { get; init; }
    public required List<Order> Recent { get; init; }
}

public class CustomerRow
{
    public required AppUser User { get; init; }
    public int Orders { get; init; }
    public decimal Spent { get; init; }
}
