using FluentAssertions;
using GameServer.Grains.Interfaces;
using GameServer.Tests.Integration.Infrastructure;

namespace GameServer.Tests.Integration.Grains;

public class ShipGrainTests : GrainTestBase
{
    [Fact]
    public async Task Initialize_ShouldSetAllState()
    {
        var grain = Cluster.GrainFactory.GetGrain<IShipGrain>(Guid.NewGuid());
        var ownerId = Guid.NewGuid();

        await grain.InitializeAsync(ownerId, "Cruiser", 200, 100, 150);
        var state = await grain.GetStateAsync();

        state.OwnerId.Should().Be(ownerId);
        state.ShipType.Should().Be("Cruiser");
        state.HullPoints.Should().Be(200);
        state.MaxHull.Should().Be(200);
        state.ShieldPoints.Should().Be(100);
        state.MaxShield.Should().Be(100);
        state.ArmorPoints.Should().Be(150);
        state.MaxArmor.Should().Be(150);
        state.IsDestroyed.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyDamage_ShieldOnly_ShouldAbsorb()
    {
        var grain = Cluster.GrainFactory.GetGrain<IShipGrain>(Guid.NewGuid());
        await grain.InitializeAsync(Guid.NewGuid(), "Frigate", 100, 50, 75);

        await grain.ApplyDamageAsync(30);
        var state = await grain.GetStateAsync();

        state.ShieldPoints.Should().Be(20);
        state.ArmorPoints.Should().Be(75);
        state.HullPoints.Should().Be(100);
    }

    [Fact]
    public async Task ApplyDamage_ShieldAndArmor_ShouldPenetrate()
    {
        var grain = Cluster.GrainFactory.GetGrain<IShipGrain>(Guid.NewGuid());
        await grain.InitializeAsync(Guid.NewGuid(), "Cruiser", 100, 50, 75);

        await grain.ApplyDamageAsync(70);
        var state = await grain.GetStateAsync();

        state.ShieldPoints.Should().Be(0);
        state.ArmorPoints.Should().Be(55);
        state.HullPoints.Should().Be(100);
    }

    [Fact]
    public async Task ApplyDamage_Destroy_ShouldSetIsDestroyed()
    {
        var grain = Cluster.GrainFactory.GetGrain<IShipGrain>(Guid.NewGuid());
        await grain.InitializeAsync(Guid.NewGuid(), "Frigate", 100, 50, 75);

        await grain.ApplyDamageAsync(300);
        var destroyed = await grain.IsDestroyedAsync();

        destroyed.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyDamage_MultipleTimes_ShouldAccumulate()
    {
        var grain = Cluster.GrainFactory.GetGrain<IShipGrain>(Guid.NewGuid());
        await grain.InitializeAsync(Guid.NewGuid(), "Frigate", 100, 50, 75);

        await grain.ApplyDamageAsync(30);
        await grain.ApplyDamageAsync(30);
        var state = await grain.GetStateAsync();

        state.ShieldPoints.Should().Be(0);
        state.ArmorPoints.Should().Be(65);
    }

    [Fact]
    public async Task RepairShield_ShouldRestore()
    {
        var grain = Cluster.GrainFactory.GetGrain<IShipGrain>(Guid.NewGuid());
        await grain.InitializeAsync(Guid.NewGuid(), "Frigate", 100, 50, 75);
        await grain.ApplyDamageAsync(20);

        await grain.RepairShieldAsync(10);
        var state = await grain.GetStateAsync();

        state.ShieldPoints.Should().Be(40);
    }

    [Fact]
    public async Task RepairShield_ShouldNotExceedMax()
    {
        var grain = Cluster.GrainFactory.GetGrain<IShipGrain>(Guid.NewGuid());
        await grain.InitializeAsync(Guid.NewGuid(), "Frigate", 100, 50, 75);
        await grain.ApplyDamageAsync(20);

        await grain.RepairShieldAsync(100);
        var state = await grain.GetStateAsync();

        state.ShieldPoints.Should().Be(50);
    }

    [Fact]
    public async Task SetLocation_ShouldPersist()
    {
        var grain = Cluster.GrainFactory.GetGrain<IShipGrain>(Guid.NewGuid());
        await grain.InitializeAsync(Guid.NewGuid(), "Frigate", 100, 50, 75);

        await grain.SetLocationAsync("jita-system");
        var state = await grain.GetStateAsync();

        state.CurrentSolarSystemId.Should().Be("jita-system");
    }

    [Fact]
    public async Task IsDestroyed_WhenNotDestroyed_ShouldBeFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IShipGrain>(Guid.NewGuid());
        await grain.InitializeAsync(Guid.NewGuid(), "Frigate", 100, 50, 75);

        var destroyed = await grain.IsDestroyedAsync();

        destroyed.Should().BeFalse();
    }
}
