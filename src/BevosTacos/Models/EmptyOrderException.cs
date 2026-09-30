namespace BevosTacos.Models;

//Thrown when an order is checked out without any items
public class EmptyOrderException : Exception
{
    public EmptyOrderException()
        : base("Order must contain at least one item.")
    {
    }
}
