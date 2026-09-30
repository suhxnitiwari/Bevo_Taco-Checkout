using System.ComponentModel.DataAnnotations;

namespace BevosTacos.Models;

public enum MenuCategory
{
    Tacos,
    Burgers,
    Sides,
    Drinks
}

//Something on the truck's menu. Managers can change the price or mark it sold out.
public class MenuItem
{
    public int MenuItemId { get; set; }

    [Required, StringLength(60)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string Description { get; set; } = string.Empty;

    public MenuCategory Category { get; set; }

    [Range(typeof(decimal), "0.25", "100", ErrorMessage = "Price must be between $0.25 and $100.")]
    [DataType(DataType.Currency)]
    public decimal Price { get; set; }

    [StringLength(8)]
    public string Emoji { get; set; } = "🌮";

    [Display(Name = "Available")]
    public bool IsAvailable { get; set; } = true;

    public bool IsVegetarian { get; set; }
    public bool IsSpicy { get; set; }

    public int SortOrder { get; set; }

    //Add-ons a customer may put on this item (many-to-many)
    public List<AddOn> AddOns { get; set; } = new();
}

//An extra that changes an item's price, like guac or a double patty
public class AddOn
{
    public int AddOnId { get; set; }

    [Required, StringLength(40)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "20")]
    public decimal Price { get; set; }

    public List<MenuItem> MenuItems { get; set; } = new();
}
