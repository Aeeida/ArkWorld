using FluentAssertions;
using GameServer.Grains.Interfaces;
using GameServer.Tests.Integration.Infrastructure;

namespace GameServer.Tests.Integration.Grains;

public class MarketGrainTests : GrainTestBase
{
    [Fact]
    public async Task PlaceOrder_ShouldReturnOrderId()
    {
        var grain = Cluster.GrainFactory.GetGrain<IMarketGrain>("jita-market");
        var sellerId = Guid.NewGuid();

        var orderId = await grain.PlaceOrderAsync(sellerId, "Tritanium", 100, 5.5m, false);

        orderId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task PlaceOrder_ShouldPersistOrder()
    {
        var grain = Cluster.GrainFactory.GetGrain<IMarketGrain>("amarr-market");
        var sellerId = Guid.NewGuid();

        await grain.PlaceOrderAsync(sellerId, "Tritanium", 100, 5.5m, false);

        var orders = await grain.GetOrdersAsync();
        orders.Should().HaveCount(1);
        orders[0].ItemId.Should().Be("Tritanium");
        orders[0].Quantity.Should().Be(100);
        orders[0].PricePerUnit.Should().Be(5.5m);
        orders[0].IsBuyOrder.Should().BeFalse();
    }

    [Fact]
    public async Task PlaceOrder_MultipleOrders_ShouldTrackAll()
    {
        var grain = Cluster.GrainFactory.GetGrain<IMarketGrain>("dodixie-market");

        await grain.PlaceOrderAsync(Guid.NewGuid(), "Tritanium", 100, 5m, false);
        await grain.PlaceOrderAsync(Guid.NewGuid(), "Pyerite", 50, 10m, false);
        await grain.PlaceOrderAsync(Guid.NewGuid(), "Mexallon", 200, 25m, true);

        var orders = await grain.GetOrdersAsync();
        orders.Should().HaveCount(3);
    }

    [Fact]
    public async Task PlaceOrder_BuyOrder_ShouldSetFlag()
    {
        var grain = Cluster.GrainFactory.GetGrain<IMarketGrain>("rens-market");

        await grain.PlaceOrderAsync(Guid.NewGuid(), "Tritanium", 1000, 4m, true);

        var orders = await grain.GetOrdersAsync();
        orders[0].IsBuyOrder.Should().BeTrue();
    }

    [Fact]
    public async Task CancelOrder_Existing_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<IMarketGrain>("hek-market");
        var orderId = await grain.PlaceOrderAsync(Guid.NewGuid(), "Tritanium", 100, 5m, false);

        var result = await grain.CancelOrderAsync(orderId);

        result.Should().BeTrue();
        var orders = await grain.GetOrdersAsync();
        orders.Should().BeEmpty();
    }

    [Fact]
    public async Task CancelOrder_NonExistent_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IMarketGrain>("random-market");

        var result = await grain.CancelOrderAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CancelOrder_ShouldOnlyRemoveSpecified()
    {
        var grain = Cluster.GrainFactory.GetGrain<IMarketGrain>("multi-cancel-market");
        var orderId1 = await grain.PlaceOrderAsync(Guid.NewGuid(), "Tritanium", 100, 5m, false);
        await grain.PlaceOrderAsync(Guid.NewGuid(), "Pyerite", 50, 10m, false);

        await grain.CancelOrderAsync(orderId1);

        var orders = await grain.GetOrdersAsync();
        orders.Should().HaveCount(1);
        orders[0].ItemId.Should().Be("Pyerite");
    }

    [Fact]
    public async Task GetOrders_WithFilter_ShouldFilterByItem()
    {
        var grain = Cluster.GrainFactory.GetGrain<IMarketGrain>("filter-market");
        await grain.PlaceOrderAsync(Guid.NewGuid(), "Tritanium", 100, 5m, false);
        await grain.PlaceOrderAsync(Guid.NewGuid(), "Tritanium", 200, 6m, false);
        await grain.PlaceOrderAsync(Guid.NewGuid(), "Pyerite", 50, 10m, false);

        var filtered = await grain.GetOrdersAsync("Tritanium");

        filtered.Should().HaveCount(2);
        filtered.Should().OnlyContain(o => o.ItemId == "Tritanium");
    }

    [Fact]
    public async Task GetOrders_WithNoMatchFilter_ShouldReturnEmpty()
    {
        var grain = Cluster.GrainFactory.GetGrain<IMarketGrain>("nomatch-market");
        await grain.PlaceOrderAsync(Guid.NewGuid(), "Tritanium", 100, 5m, false);

        var filtered = await grain.GetOrdersAsync("NonExistentItem");

        filtered.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrders_NullFilter_ShouldReturnAll()
    {
        var grain = Cluster.GrainFactory.GetGrain<IMarketGrain>("allorders-market");
        await grain.PlaceOrderAsync(Guid.NewGuid(), "Tritanium", 100, 5m, false);
        await grain.PlaceOrderAsync(Guid.NewGuid(), "Pyerite", 50, 10m, false);

        var all = await grain.GetOrdersAsync(null);

        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task PlaceOrder_ShouldHaveExpiresAt()
    {
        var grain = Cluster.GrainFactory.GetGrain<IMarketGrain>("expiry-market");

        await grain.PlaceOrderAsync(Guid.NewGuid(), "Tritanium", 100, 5m, false);

        var orders = await grain.GetOrdersAsync();
        orders[0].ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }
}
