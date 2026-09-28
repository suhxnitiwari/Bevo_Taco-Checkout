using System.ComponentModel.DataAnnotations;

namespace Tiwari_Suhani_HW2.Models;

public enum CustomerType // Customer Type can only be one of two things: Walkup or Catering
{
    Walkup,
    Catering
}

// holds the stuff that's true for ANY order 
public abstract class Order 
{

    //Price charged per taco
    public const decimal TACO_PRICE = 2.75m;

    //Price charged per burger
    public const decimal BURGER_PRICE = 4.50m;


    //Stores whether this order is a Walkup or Catering order
    [Display(Name = "Customer Type")]
    public CustomerType CustomerType { get; set; }

    //Number of tacos the customer ordered - cannot be negative
    [Display(Name = "Number of Tacos")]
    [Range(0, int.MaxValue, ErrorMessage = "Number of tacos cannot be negative.")]
    public int NumberOfTacos { get; set; }

    //Number of burgers the customer ordered - cannot be negative
    [Display(Name = "Number of Burgers")]
    [Range(0, int.MaxValue, ErrorMessage = "Number of burgers cannot be negative.")]
    public int NumberOfBurgers { get; set; }

    //Total number of items ordered (tacos + burgers)
    [Display(Name = "Total Items")]
    public int TotalItems { get; set; }

    //Subtotal for just the tacos (NumberOfTacos * TACO_PRICE)
    [Display(Name = "Taco Subtotal")]
    public decimal TacoSubtotal { get; set; }

    //Subtotal for just the burgers (NumberOfBurgers * BURGER_PRICE)
    [Display(Name = "Burger Subtotal")]
    public decimal BurgerSubtotal { get; set; }

    //Combined subtotal (TacoSubtotal + BurgerSubtotal)
    [Display(Name = "Subtotal")]
    public decimal Subtotal { get; set; }

    //Final total charged to the customer
    //Walkup orders: Subtotal + Tax   |   Catering orders: Subtotal + DeliveryFee
    [Display(Name = "Total")]
    public decimal Total { get; set; }

    //----- Methods -----

    //Calculates TotalItems, TacoSubtotal, BurgerSubtotal, and Subtotal
    //Throws an exception if the customer did not order any items
    protected void CalcSubtotals()
    {
        TotalItems = NumberOfTacos + NumberOfBurgers;

        //Business rule: an order must contain at least one taco or burger
        if (TotalItems == 0)
        {
            throw new Exception("Order must contain at least one taco or burger.");
        }

        TacoSubtotal = NumberOfTacos * TACO_PRICE;
        BurgerSubtotal = NumberOfBurgers * BURGER_PRICE;
        Subtotal = TacoSubtotal + BurgerSubtotal;
    }
}
