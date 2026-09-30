using System.ComponentModel.DataAnnotations;

namespace BevosTacos.Models;

public enum CustomerType
{
    Walkup,
    Catering
}

public enum OrderStatus
{
    New,
    Cooking,
    Ready,
    Complete,
    Cancelled
}

//Shared pricing and validation for every kind of order.
//Stored in one Orders table (table-per-hierarchy) with a discriminator for the subclass.
public abstract class Order
{
    //Order numbers shown to customers start at 1001
    public const int OrderNumberOffset = 1000;

    public int OrderId { get; set; }

    [Display(Name = "Order #")]
    public int OrderNumber => OrderId + OrderNumberOffset;

    //Each subclass reports its own customer type
    [Display(Name = "Customer Type")]
    public abstract CustomerType CustomerType { get; }

    [Display(Name = "Placed")]
    public DateTime PlacedAtUtc { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.New;

    //When the order last changed status, for the kitchen board
    public DateTime StatusChangedAtUtc { get; set; }

    //Null for walk-up guests who order without an account
    public string? CustomerId { get; set; }
    public AppUser? Customer { get; set; }

    public List<OrderLine> Lines { get; set; } = new();

    [Display(Name = "Total Items")]
    public int TotalItems { get; private set; }

    [Display(Name = "Subtotal")]
    public decimal Subtotal { get; private set; }

    //Walkup orders: Subtotal + SalesTax + Tip   |   Catering orders: Subtotal + DeliveryFee
    [Display(Name = "Total")]
    public decimal Total { get; protected set; }

    //Calculates the subtotal, then lets the subclass apply its own charges to get the Total
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
        TotalItems = Lines.Sum(l => l.Quantity);

        if (TotalItems == 0)
        {
            throw new EmptyOrderException();
        }

        Subtotal = Lines.Sum(l => l.LineTotal);
    }

    //Moves the order one step through the kitchen; returns false if it can't move
    public bool Advance(DateTime nowUtc)
    {
        if (Status is OrderStatus.Complete or OrderStatus.Cancelled)
        {
            return false;
        }

        Status += 1;
        StatusChangedAtUtc = nowUtc;
        return true;
    }

    //Orders can only be cancelled before the kitchen starts cooking
    public bool Cancel(DateTime nowUtc)
    {
        if (Status != OrderStatus.New)
        {
            return false;
        }

        Status = OrderStatus.Cancelled;
        StatusChangedAtUtc = nowUtc;
        return true;
    }
}
