using BevosTacos.Models;
using static BevosTacos.Tests.OrderLines;

namespace BevosTacos.Tests;

public class OrderStatusTests
{
    private static readonly DateTime Now = new(2026, 9, 30, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Advance_MovesThroughTheKitchenInOrder()
    {
        var order = new WalkupOrder { Lines = Of(Tacos(1)) };

        Assert.True(order.Advance(Now));
        Assert.Equal(OrderStatus.Cooking, order.Status);
        Assert.True(order.Advance(Now));
        Assert.Equal(OrderStatus.Ready, order.Status);
        Assert.True(order.Advance(Now));
        Assert.Equal(OrderStatus.Complete, order.Status);
        Assert.Equal(Now, order.StatusChangedAtUtc);
    }

    [Theory]
    [InlineData(OrderStatus.Complete)]
    [InlineData(OrderStatus.Cancelled)]
    public void Advance_StopsAtTheEnd(OrderStatus status)
    {
        var order = new WalkupOrder { Status = status };

        Assert.False(order.Advance(Now));
        Assert.Equal(status, order.Status);
    }

    [Fact]
    public void Cancel_WorksOnlyBeforeCooking()
    {
        var order = new WalkupOrder();
        Assert.True(order.Cancel(Now));
        Assert.Equal(OrderStatus.Cancelled, order.Status);

        var cooking = new WalkupOrder { Status = OrderStatus.Cooking };
        Assert.False(cooking.Cancel(Now));
        Assert.Equal(OrderStatus.Cooking, cooking.Status);
    }

    [Fact]
    public void OrderNumber_StartsAt1001()
    {
        Assert.Equal(1001, new WalkupOrder { OrderId = 1 }.OrderNumber);
    }
}
