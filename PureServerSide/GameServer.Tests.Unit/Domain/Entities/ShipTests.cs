using FluentAssertions;
using GameServer.Domain.Entities;

namespace GameServer.Tests.Unit.Domain.Entities;

public class ShipTests
{
    private static Ship CreateDefault(double hull = 100, double shield = 50, double armor = 75) =>
        Ship.Create(Guid.NewGuid(), Guid.NewGuid(), "Frigate", hull, shield, armor);

    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var ship = Ship.Create(id, ownerId, "Cruiser", 200, 100, 150);

        ship.Id.Should().Be(id);
        ship.OwnerId.Should().Be(ownerId);
        ship.ShipType.Should().Be("Cruiser");
        ship.HullPoints.Should().Be(200);
        ship.MaxHull.Should().Be(200);
        ship.ShieldPoints.Should().Be(100);
        ship.MaxShield.Should().Be(100);
        ship.ArmorPoints.Should().Be(150);
        ship.MaxArmor.Should().Be(150);
        ship.IsDestroyed.Should().BeFalse();
    }

    [Fact]
    public void ApplyDamage_ShieldOnly_ShouldReduceShield()
    {
        var ship = CreateDefault();

        ship.ApplyDamage(30);

        ship.ShieldPoints.Should().Be(20);
        ship.ArmorPoints.Should().Be(75);
        ship.HullPoints.Should().Be(100);
    }

    [Fact]
    public void ApplyDamage_ShieldAndArmor_ShouldPenetrate()
    {
        var ship = CreateDefault();

        ship.ApplyDamage(70);

        ship.ShieldPoints.Should().Be(0);
        ship.ArmorPoints.Should().Be(55);
        ship.HullPoints.Should().Be(100);
    }

    [Fact]
    public void ApplyDamage_ThroughAllLayers_ShouldReduceHull()
    {
        var ship = CreateDefault(); // shield=50, armor=75, hull=100

        ship.ApplyDamage(150); // 50+75+25=150

        ship.ShieldPoints.Should().Be(0);
        ship.ArmorPoints.Should().Be(0);
        ship.HullPoints.Should().Be(75);
        ship.IsDestroyed.Should().BeFalse();
    }

    [Fact]
    public void ApplyDamage_Massive_ShouldDestroy()
    {
        var ship = CreateDefault();

        ship.ApplyDamage(300);

        ship.IsDestroyed.Should().BeTrue();
        ship.HullPoints.Should().Be(0);
    }

    [Fact]
    public void ApplyDamage_ExactTotal_ShouldDestroy()
    {
        var ship = CreateDefault(); // 50+75+100=225

        ship.ApplyDamage(225);

        ship.IsDestroyed.Should().BeTrue();
    }

    [Fact]
    public void ApplyDamage_Zero_ShouldNotChange()
    {
        var ship = CreateDefault();

        ship.ApplyDamage(0);

        ship.ShieldPoints.Should().Be(50);
        ship.ArmorPoints.Should().Be(75);
        ship.HullPoints.Should().Be(100);
    }

    [Fact]
    public void ApplyDamage_MultipleTimes_ShouldAccumulate()
    {
        var ship = CreateDefault();

        ship.ApplyDamage(30);
        ship.ApplyDamage(30);

        ship.ShieldPoints.Should().Be(0);
        ship.ArmorPoints.Should().Be(65);
        ship.HullPoints.Should().Be(100);
    }

    [Fact]
    public void RepairShield_ShouldIncrease()
    {
        var ship = CreateDefault();
        ship.ApplyDamage(20);

        ship.RepairShield(10);

        ship.ShieldPoints.Should().Be(40);
    }

    [Fact]
    public void RepairShield_ShouldNotExceedMax()
    {
        var ship = CreateDefault();
        ship.ApplyDamage(20);

        ship.RepairShield(100);

        ship.ShieldPoints.Should().Be(50);
    }

    [Fact]
    public void RepairShield_AtMax_ShouldNotChange()
    {
        var ship = CreateDefault();

        ship.RepairShield(50);

        ship.ShieldPoints.Should().Be(50);
    }

    [Fact]
    public void IsDestroyed_WhenHullAboveZero_ShouldBeFalse()
    {
        var ship = CreateDefault();

        ship.IsDestroyed.Should().BeFalse();
    }

    [Fact]
    public void CurrentSolarSystemId_DefaultNull()
    {
        var ship = CreateDefault();

        ship.CurrentSolarSystemId.Should().BeNull();
    }

    [Fact]
    public void CurrentSolarSystemId_CanSet()
    {
        var ship = CreateDefault();

        ship.CurrentSolarSystemId = "jita";

        ship.CurrentSolarSystemId.Should().Be("jita");
    }
}
