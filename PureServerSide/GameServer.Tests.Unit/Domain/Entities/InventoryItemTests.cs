using FluentAssertions;
using GameServer.Domain.Entities;

namespace GameServer.Tests.Unit.Domain.Entities;

public class InventoryItemTests
{
    [Fact]
    public void AddQuantity_ShouldIncrease()
    {
        var item = new InventoryItem { ItemId = "ore-veldspar", OwnerId = Guid.NewGuid(), Rarity = "Common", Quantity = 10 };

        item.AddQuantity(5);

        item.Quantity.Should().Be(15);
    }

    [Fact]
    public void AddQuantity_MultipleAdds_ShouldAccumulate()
    {
        var item = new InventoryItem { ItemId = "ore-veldspar", OwnerId = Guid.NewGuid(), Rarity = "Common", Quantity = 0 };

        item.AddQuantity(10);
        item.AddQuantity(20);
        item.AddQuantity(30);

        item.Quantity.Should().Be(60);
    }

    [Fact]
    public void RemoveQuantity_Sufficient_ShouldSucceed()
    {
        var item = new InventoryItem { ItemId = "ore-veldspar", OwnerId = Guid.NewGuid(), Rarity = "Common", Quantity = 50 };

        var result = item.RemoveQuantity(20);

        result.Should().BeTrue();
        item.Quantity.Should().Be(30);
    }

    [Fact]
    public void RemoveQuantity_Insufficient_ShouldFail()
    {
        var item = new InventoryItem { ItemId = "ore-veldspar", OwnerId = Guid.NewGuid(), Rarity = "Common", Quantity = 10 };

        var result = item.RemoveQuantity(20);

        result.Should().BeFalse();
        item.Quantity.Should().Be(10);
    }

    [Fact]
    public void RemoveQuantity_Exact_ShouldSucceed()
    {
        var item = new InventoryItem { ItemId = "ore-veldspar", OwnerId = Guid.NewGuid(), Rarity = "Common", Quantity = 10 };

        var result = item.RemoveQuantity(10);

        result.Should().BeTrue();
        item.Quantity.Should().Be(0);
    }

    [Fact]
    public void Properties_ShouldBeSet()
    {
        var ownerId = Guid.NewGuid();
        var item = new InventoryItem { ItemId = "laser-mk2", OwnerId = ownerId, Rarity = "Rare", Quantity = 1 };

        item.ItemId.Should().Be("laser-mk2");
        item.OwnerId.Should().Be(ownerId);
        item.Rarity.Should().Be("Rare");
        item.Quantity.Should().Be(1);
    }
}
