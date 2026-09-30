using BevosTacos.Data;
using BevosTacos.Models;
using Microsoft.EntityFrameworkCore;

namespace BevosTacos.Services;

//The cart turned into priced order lines, plus anything that can't be ordered right now
public record PricedCart(List<OrderLine> Lines, List<string> Problems, Dictionary<string, OrderLine> ByKey)
{
    public decimal Subtotal => Lines.Sum(l => l.LineTotal);
    public int ItemCount => Lines.Sum(l => l.Quantity);
}

public class OrderService(AppDbContext db, TimeProvider clock)
{
    //Looks up current prices and availability for every cart line
    public async Task<PricedCart> PriceCartAsync(IReadOnlyList<CartLine> cart)
    {
        var ids = cart.Select(c => c.MenuItemId).Distinct().ToList();
        var items = await db.MenuItems.Include(m => m.AddOns).Where(m => ids.Contains(m.MenuItemId)).ToDictionaryAsync(m => m.MenuItemId);

        var lines = new List<OrderLine>();
        var problems = new List<string>();
        var byKey = new Dictionary<string, OrderLine>();

        foreach (var c in cart)
        {
            if (!items.TryGetValue(c.MenuItemId, out var item))
            {
                problems.Add("An item in your cart is no longer on the menu.");
                continue;
            }
            if (!item.IsAvailable)
            {
                problems.Add($"{item.Name} is sold out right now.");
                continue;
            }

            //Ignore add-ons that don't belong to this item instead of trusting the form
            var addOns = item.AddOns.Where(a => c.AddOnIds.Contains(a.AddOnId)).OrderBy(a => a.Name).ToList();
            if (addOns.Count != c.AddOnIds.Length)
            {
                problems.Add($"Some add-ons aren't available on {item.Name}.");
            }

            var line = new OrderLine
            {
                MenuItemId = item.MenuItemId,
                MenuItem = item,
                ItemName = item.Name,
                Category = item.Category,
                AddOnSummary = string.Join(", ", addOns.Select(a => a.Name)),
                UnitPrice = item.Price + addOns.Sum(a => a.Price),
                Quantity = c.Quantity
            };
            lines.Add(line);
            byKey[c.Key] = line;
        }

        return new PricedCart(lines, problems, byKey);
    }

    //Prices and saves an order. Lines must come from PriceCartAsync.
    public async Task<Order> PlaceAsync(Order order, IEnumerable<OrderLine> lines)
    {
        //Don't re-insert the menu item rows that came along with the priced lines
        order.Lines = lines.Select(l => new OrderLine
        {
            MenuItemId = l.MenuItemId,
            ItemName = l.ItemName,
            Category = l.Category,
            AddOnSummary = l.AddOnSummary,
            UnitPrice = l.UnitPrice,
            Quantity = l.Quantity
        }).ToList();

        order.CalcTotals();
        var now = clock.GetUtcNow().UtcDateTime;
        order.PlacedAtUtc = now;
        order.StatusChangedAtUtc = now;
        order.Status = OrderStatus.New;

        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    public Task<Order?> FindAsync(int orderId) =>
        db.Orders.Include(o => o.Lines).Include(o => o.Customer).FirstOrDefaultAsync(o => o.OrderId == orderId);
}
