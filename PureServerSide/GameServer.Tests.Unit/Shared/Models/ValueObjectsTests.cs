using FluentAssertions;
using Game.Shared.Models;

namespace GameServer.Tests.Unit.Shared.Models;

public class Vector3DTests
{
    [Fact]
    public void DistanceTo_KnownValues_ShouldBeCorrect()
    {
        var a = new Vector3D(0, 0, 0);
        var b = new Vector3D(3, 4, 0);

        a.DistanceTo(b).Should().Be(5);
    }

    [Fact]
    public void DistanceTo_SamePoint_ShouldBeZero()
    {
        var a = new Vector3D(5, 5, 5);

        a.DistanceTo(a).Should().Be(0);
    }

    [Fact]
    public void DistanceTo_3D_ShouldBeCorrect()
    {
        var a = new Vector3D(0, 0, 0);
        var b = new Vector3D(1, 2, 2);

        a.DistanceTo(b).Should().Be(3);
    }

    [Fact]
    public void Addition_ShouldWork()
    {
        var a = new Vector3D(1, 2, 3);
        var b = new Vector3D(4, 5, 6);

        var result = a + b;

        result.Should().Be(new Vector3D(5, 7, 9));
    }

    [Fact]
    public void Subtraction_ShouldWork()
    {
        var a = new Vector3D(5, 7, 9);
        var b = new Vector3D(1, 2, 3);

        var result = a - b;

        result.Should().Be(new Vector3D(4, 5, 6));
    }

    [Fact]
    public void ScalarMultiplication_ShouldWork()
    {
        var v = new Vector3D(1, 2, 3);

        var result = v * 2;

        result.Should().Be(new Vector3D(2, 4, 6));
    }

    [Fact]
    public void Normalize_ShouldReturnUnitVector()
    {
        var v = new Vector3D(3, 0, 0);

        var n = v.Normalize();

        n.X.Should().BeApproximately(1.0, 0.001);
        n.Y.Should().BeApproximately(0.0, 0.001);
        n.Z.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void Normalize_DiagonalVector_ShouldHaveLengthOne()
    {
        var v = new Vector3D(1, 1, 1);

        var n = v.Normalize();
        var length = Math.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z);

        length.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void Normalize_ZeroVector_ShouldReturnZero()
    {
        var v = Vector3D.Zero;

        var n = v.Normalize();

        n.Should().Be(Vector3D.Zero);
    }

    [Fact]
    public void Zero_ShouldBeOrigin()
    {
        Vector3D.Zero.X.Should().Be(0);
        Vector3D.Zero.Y.Should().Be(0);
        Vector3D.Zero.Z.Should().Be(0);
    }

    [Fact]
    public void RecordEquality_ShouldWork()
    {
        var a = new Vector3D(1, 2, 3);
        var b = new Vector3D(1, 2, 3);

        a.Should().Be(b);
    }
}

public class CurrencyTests
{
    [Fact]
    public void Addition_SameCurrency_ShouldWork()
    {
        var a = new Currency(100m, "ISK");
        var b = new Currency(50m, "ISK");

        var result = a + b;

        result.Amount.Should().Be(150m);
        result.CurrencyCode.Should().Be("ISK");
    }

    [Fact]
    public void Addition_DifferentCurrency_ShouldThrow()
    {
        var a = new Currency(100m, "ISK");
        var b = new Currency(50m, "USD");

        var act = () => a + b;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Subtraction_SameCurrency_ShouldWork()
    {
        var a = new Currency(100m, "ISK");
        var b = new Currency(30m, "ISK");

        var result = a - b;

        result.Amount.Should().Be(70m);
    }

    [Fact]
    public void Subtraction_DifferentCurrency_ShouldThrow()
    {
        var a = new Currency(100m, "ISK");
        var b = new Currency(50m, "USD");

        var act = () => a - b;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Multiplication_ShouldWork()
    {
        var c = new Currency(10m, "ISK");

        var result = c * 5;

        result.Amount.Should().Be(50m);
    }

    [Fact]
    public void CanAfford_Sufficient_ShouldBeTrue()
    {
        var wallet = new Currency(1000m, "ISK");
        var cost = new Currency(500m, "ISK");

        wallet.CanAfford(cost).Should().BeTrue();
    }

    [Fact]
    public void CanAfford_Insufficient_ShouldBeFalse()
    {
        var wallet = new Currency(100m, "ISK");
        var cost = new Currency(500m, "ISK");

        wallet.CanAfford(cost).Should().BeFalse();
    }

    [Fact]
    public void CanAfford_ExactAmount_ShouldBeTrue()
    {
        var wallet = new Currency(500m, "ISK");
        var cost = new Currency(500m, "ISK");

        wallet.CanAfford(cost).Should().BeTrue();
    }

    [Fact]
    public void CanAfford_DifferentCurrency_ShouldBeFalse()
    {
        var wallet = new Currency(1000m, "ISK");
        var cost = new Currency(100m, "USD");

        wallet.CanAfford(cost).Should().BeFalse();
    }

    [Fact]
    public void Zero_ShouldBeZero()
    {
        Currency.Zero.Amount.Should().Be(0m);
    }
}

public class SkillLevelTests
{
    [Fact]
    public void MaxLevel_ShouldBe5()
    {
        SkillLevel.MaxLevel.Should().Be(5);
    }

    [Fact]
    public void IsMaxed_AtMaxLevel_ShouldBeTrue()
    {
        var skill = new SkillLevel("gunnery", 5);

        skill.IsMaxed.Should().BeTrue();
    }

    [Fact]
    public void IsMaxed_BelowMax_ShouldBeFalse()
    {
        var skill = new SkillLevel("gunnery", 3);

        skill.IsMaxed.Should().BeFalse();
    }

    [Fact]
    public void WithLevel_ShouldClampToMax()
    {
        var skill = new SkillLevel("gunnery", 1);

        var upgraded = skill.WithLevel(10);

        upgraded.Level.Should().Be(5);
    }

    [Fact]
    public void WithLevel_ShouldClampToZero()
    {
        var skill = new SkillLevel("gunnery", 1);

        var downgraded = skill.WithLevel(-5);

        downgraded.Level.Should().Be(0);
    }

    [Fact]
    public void WithLevel_ValidLevel_ShouldSet()
    {
        var skill = new SkillLevel("gunnery", 1);

        var upgraded = skill.WithLevel(3);

        upgraded.Level.Should().Be(3);
        upgraded.SkillId.Should().Be("gunnery");
    }

    [Fact]
    public void IsTraining_ZeroTicks_ShouldBeFalse()
    {
        var skill = new SkillLevel("gunnery", 1, 0);

        skill.IsTraining.Should().BeFalse();
    }

    [Fact]
    public void IsTraining_FutureTicks_ShouldBeTrue()
    {
        var futureTicks = DateTimeOffset.UtcNow.AddHours(1).Ticks;
        var skill = new SkillLevel("gunnery", 1, futureTicks);

        skill.IsTraining.Should().BeTrue();
    }

    [Fact]
    public void IsTraining_PastTicks_ShouldBeFalse()
    {
        var pastTicks = DateTimeOffset.UtcNow.AddHours(-1).Ticks;
        var skill = new SkillLevel("gunnery", 1, pastTicks);

        skill.IsTraining.Should().BeFalse();
    }
}

public class DamageInfoTests
{
    [Fact]
    public void Properties_ShouldBeSet()
    {
        var info = new DamageInfo(10, 20, 30, 60, false);

        info.ShieldDamage.Should().Be(10);
        info.ArmorDamage.Should().Be(20);
        info.HullDamage.Should().Be(30);
        info.TotalDamage.Should().Be(60);
        info.IsKillingBlow.Should().BeFalse();
    }

    [Fact]
    public void KillingBlow_ShouldBeTrue()
    {
        var info = new DamageInfo(0, 0, 100, 100, true);

        info.IsKillingBlow.Should().BeTrue();
    }
}

public class WorldPositionTests
{
    [Fact]
    public void Properties_ShouldBeSet()
    {
        var pos = new WorldPosition("jita", new Vector3D(1, 2, 3), 45f);

        pos.WorldId.Should().Be("jita");
        pos.Position.Should().Be(new Vector3D(1, 2, 3));
        pos.Rotation.Should().Be(45f);
    }

    [Fact]
    public void DefaultRotation_ShouldBeZero()
    {
        var pos = new WorldPosition("jita", Vector3D.Zero);

        pos.Rotation.Should().Be(0f);
    }
}

public class InventorySlotTests
{
    [Fact]
    public void Properties_ShouldBeSet()
    {
        var slot = new InventorySlot(0, "laser-mk2", 5, "Rare");

        slot.SlotIndex.Should().Be(0);
        slot.ItemId.Should().Be("laser-mk2");
        slot.Quantity.Should().Be(5);
        slot.Rarity.Should().Be("Rare");
    }
}
