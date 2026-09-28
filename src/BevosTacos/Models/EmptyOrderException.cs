namespace BevosTacos.Models;

//Thrown when an order is checked out without any tacos or burgers
public class EmptyOrderException : Exception
{
    public EmptyOrderException()
        : base("Order must contain at least one taco or burger.")
    {
    }
}
