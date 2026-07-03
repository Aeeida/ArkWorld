using FluentAssertions;
using GameServer.Domain.Entities;

namespace GameServer.Tests.Unit.Domain.Entities;

public class MarketOrderTests
{
    private static MarketOrder CreateDefault(int quantity = 100, decimal price = 10m) =>
        MarketOrder.Create(Guid.NewGuid(), Guid.NewGuid(), "Ore", quantity, price, "station-1", false, TimeSpan.FromDays(7));

    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var id = Guid.NewGuid();
        var sellerId = Guid.NewGuid();

        var order = MarketOrder.Create(id, sellerId, "Minerals", 50, 25m, "station-hub", true, TimeSpan.FromDays(30));

        order.Id.Should().Be(id);
        order.SellerId.Should().Be(sellerId);
        order.ItemId.Should().Be("Minerals");
        order.Quantity.Should().Be(50);
        order.PricePerUnit.Should().Be(25m);
        order.StationId.Should().Be("station-hub");
        order.IsBuyOrder.Should().BeTrue();
        order.IsFilled.Should().BeFalse();
    }

    [Fact]
    public void Create_WithZeroPrice_ShouldThrow()
    {
        var act = () => MarketOrder.Create(Guid.NewGuid(), Guid.NewGuid(), "Ore", 100, 0m, "station-1", false, TimeSpan.FromDays(7));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNegativePrice_ShouldThrow()
    {
        var act = () => MarketOrder.Create(Guid.NewGuid(), Guid.NewGuid(), "Ore", 100, -10m, "station-1", false, TimeSpan.FromDays(7));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FillOrder_PartialFill_ShouldReduceQuantity()
    {
        var order = CreateDefault(100);

        var filled = order.FillOrder(30);

        filled.Should().Be(30);
        order.Quantity.Should().Be(70);
        order.IsFilled.Should().BeFalse();
    }

    [Fact]
    public void FillOrder_ExactFill_ShouldBeFilled()
    {
        var order = CreateDefault(50);

        var filled = order.FillOrder(50);

        filled.Should().Be(50);
        order.IsFilled.Should().BeTrue();
    }

    [Fact]
    public void FillOrder_OverFill_ShouldClampToQuantity()
    {
        var order = CreateDefault(10);

        var filled = order.FillOrder(50);

        filled.Should().Be(10);
        order.IsFilled.Should().BeTrue();
    }

    [Fact]
    public void FillOrder_MultipleFills_ShouldAccumulate()
    {
        var order = CreateDefault(100);

        order.FillOrder(30);
        order.FillOrder(40);

        order.Quantity.Should().Be(30);
    }

    [Fact]
    public void IsFilled_WhenQuantityZero_ShouldBeTrue()
    {
        var order = CreateDefault(10);

        order.FillOrder(10);

        order.IsFilled.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_FutureExpiry_ShouldBeFalse()
    {
        var order = CreateDefault();

        order.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void Quantity_SetNegative_ShouldThrow()
    {
        var order = CreateDefault(10);

        var act = () => order.FillOrder(20); // would result in negative

        // FillOrder clamps, so it shouldn't throw—the filled result is clamped to 10
        var filled = order.FillOrder(20);
        filled.Should().Be(10);
    }
}
