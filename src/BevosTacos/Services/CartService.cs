using System.Text.Json;

namespace BevosTacos.Services;

//One row in the cart. Only IDs and a quantity are kept; prices always come from the database.
public record CartLine(int MenuItemId, int[] AddOnIds, int Quantity)
{
    public string Key => $"{MenuItemId}:{string.Join('-', AddOnIds)}";
}

//Keeps the cart in the user's session so guests can order without an account
public class CartService(IHttpContextAccessor http)
{
    private const string SessionKey = "cart";
    public const int MaxQuantityPerLine = 500;

    private ISession Session => http.HttpContext?.Session ?? throw new InvalidOperationException("No active session.");

    public List<CartLine> GetLines()
    {
        var json = Session.GetString(SessionKey);
        return json is null ? new() : JsonSerializer.Deserialize<List<CartLine>>(json) ?? new();
    }

    public int Count() => GetLines().Sum(l => l.Quantity);

    //Adds to an existing line when the item and add-ons match
    public void Add(int menuItemId, IEnumerable<int> addOnIds, int quantity)
    {
        var line = new CartLine(menuItemId, addOnIds.Distinct().Order().ToArray(), Math.Clamp(quantity, 1, MaxQuantityPerLine));
        var lines = GetLines();
        var index = lines.FindIndex(l => l.Key == line.Key);
        if (index >= 0)
        {
            lines[index] = lines[index] with { Quantity = Math.Min(lines[index].Quantity + line.Quantity, MaxQuantityPerLine) };
        }
        else
        {
            lines.Add(line);
        }
        Save(lines);
    }

    //Sets a line's quantity; zero or less removes it
    public void SetQuantity(string key, int quantity)
    {
        var lines = GetLines();
        var index = lines.FindIndex(l => l.Key == key);
        if (index < 0) return;
        if (quantity <= 0) lines.RemoveAt(index);
        else lines[index] = lines[index] with { Quantity = Math.Min(quantity, MaxQuantityPerLine) };
        Save(lines);
    }

    public void Clear() => Session.Remove(SessionKey);

    private void Save(List<CartLine> lines) => Session.SetString(SessionKey, JsonSerializer.Serialize(lines));
}
