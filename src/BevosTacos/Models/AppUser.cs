using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace BevosTacos.Models;

public static class Roles
{
    public const string Customer = "Customer";
    public const string Kitchen = "Kitchen";
    public const string Manager = "Manager";
    public const string Staff = Kitchen + "," + Manager;
}

//A signed-in user. Customers get a customer code for catering; staff don't need one.
public class AppUser : IdentityUser
{
    [Required, StringLength(40)]
    public string FirstName { get; set; } = string.Empty;

    //2-4 letters, unique; used on catering orders
    [StringLength(4)]
    public string? CustomerCode { get; set; }

    //Only managers can grant this; it waives catering delivery fees
    public bool PreferredCustomer { get; set; }

    public List<Order> Orders { get; set; } = new();
}
