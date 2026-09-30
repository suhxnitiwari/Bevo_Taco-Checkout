using BevosTacos.Models;

namespace BevosTacos.Tests;

//Builds order lines at the original $2.75 taco / $4.50 burger prices
internal static class OrderLines
{
    public static OrderLine Tacos(int quantity, decimal addOns = 0) =>
        new() { ItemName = "Smoked Brisket", Category = MenuCategory.Tacos, UnitPrice = 2.75m + addOns, Quantity = quantity };

    public static OrderLine Burgers(int quantity, decimal addOns = 0) =>
        new() { ItemName = "Classic Longhorn", Category = MenuCategory.Burgers, UnitPrice = 4.50m + addOns, Quantity = quantity };

    public static OrderLine Drinks(int quantity) =>
        new() { ItemName = "Topo Chico", Category = MenuCategory.Drinks, UnitPrice = 2.00m, Quantity = quantity };

    public static List<OrderLine> Of(params OrderLine[] lines) => lines.ToList();
}
