namespace BevosTacos.Models;

//One item on an order. The name, add-ons and price are copied from the menu when the
//order is placed, so later menu changes never rewrite an old receipt.
public class OrderLine
{
    public int OrderLineId { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int MenuItemId { get; set; }
    public MenuItem? MenuItem { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public MenuCategory Category { get; set; }

    //Comma-separated add-on names, e.g. "Add guac, Double patty"
    public string AddOnSummary { get; set; } = string.Empty;

    //Item price plus add-ons, at the time of the order
    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
}
