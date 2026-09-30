using BevosTacos.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BevosTacos.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options), IDataProtectionKeyContext
{
    //Keys that sign cookies and anti-forgery tokens, kept in the database so a restart doesn't sign everyone out
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<AddOn> AddOns => Set<AddOn>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<WalkupOrder> WalkupOrders => Set<WalkupOrder>();
    public DbSet<CateringOrder> CateringOrders => Set<CateringOrder>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        //One Orders table for both order types, told apart by OrderType
        builder.Entity<Order>(order =>
        {
            order.HasDiscriminator<string>("OrderType")
                .HasValue<WalkupOrder>("Walkup")
                .HasValue<CateringOrder>("Catering");
            order.Property<string>("OrderType").HasMaxLength(10);
            order.Property(o => o.Status).HasConversion<string>().HasMaxLength(12);
            order.HasIndex(o => o.Status);
            order.HasIndex(o => o.PlacedAtUtc);
            order.HasOne(o => o.Customer).WithMany(u => u.Orders).HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.SetNull);
            order.HasMany(o => o.Lines).WithOne(l => l.Order).HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CateringOrder>().Property(o => o.CustomerCode).HasMaxLength(4);

        builder.Entity<OrderLine>(line =>
        {
            line.Property(l => l.ItemName).HasMaxLength(60);
            line.Property(l => l.AddOnSummary).HasMaxLength(200);
            line.Property(l => l.Category).HasConversion<string>().HasMaxLength(10);
            //Menu items that have been ordered can't be deleted, only marked unavailable
            line.HasOne(l => l.MenuItem).WithMany().HasForeignKey(l => l.MenuItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MenuItem>(item =>
        {
            item.Property(m => m.Category).HasConversion<string>().HasMaxLength(10);
            item.HasMany(m => m.AddOns).WithMany(a => a.MenuItems).UsingEntity(j => j.ToTable("MenuItemAddOns"));
        });

        builder.Entity<AppUser>().HasIndex(u => u.CustomerCode).IsUnique();

        //Money is stored with 2 decimal places everywhere
        foreach (var property in builder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()).Where(p => p.ClrType == typeof(decimal)))
        {
            property.SetPrecision(10);
            property.SetScale(2);
        }
    }
}
