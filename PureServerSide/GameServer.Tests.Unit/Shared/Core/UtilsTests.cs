using FluentAssertions;
using Game.Shared.Core.Utils;

namespace GameServer.Tests.Unit.Shared.Core;

public class IdGeneratorTests
{
    [Fact]
    public void NewSequentialId_ShouldBeUnique()
    {
        var a = IdGenerator.NewSequentialId();
        var b = IdGenerator.NewSequentialId();

        a.Should().NotBe(b);
    }

    [Fact]
    public void NewSequentialId_ShouldNotBeEmpty()
    {
        var id = IdGenerator.NewSequentialId();

        id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void NewSequentialId_MultipleCalls_AllUnique()
    {
        var ids = Enumerable.Range(0, 100).Select(_ => IdGenerator.NewSequentialId()).ToList();

        ids.Distinct().Should().HaveCount(100);
    }

    [Fact]
    public void NewShortId_ShouldBe22Chars()
    {
        var id = IdGenerator.NewShortId();

        id.Should().HaveLength(22);
    }

    [Fact]
    public void NewShortId_ShouldBeUnique()
    {
        var a = IdGenerator.NewShortId();
        var b = IdGenerator.NewShortId();

        a.Should().NotBe(b);
    }

    [Fact]
    public void NewShortId_ShouldNotContainSlashOrPlus()
    {
        // Generate many to increase chance of hitting base64 special chars
        for (var i = 0; i < 100; i++)
        {
            var id = IdGenerator.NewShortId();
            id.Should().NotContain("/");
            id.Should().NotContain("+");
        }
    }
}

public class TimeUtilsTests
{
    [Fact]
    public void UnixNowMs_ShouldBePositive()
    {
        TimeUtils.UnixNowMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public void UnixNowSec_ShouldBePositive()
    {
        TimeUtils.UnixNowSec.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FromUnixMs_ShouldRoundTrip()
    {
        var now = DateTimeOffset.UtcNow;
        var ms = now.ToUnixTimeMilliseconds();

        var result = TimeUtils.FromUnixMs(ms);

        result.Should().BeCloseTo(now.UtcDateTime, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void Since_ShouldReturnPositiveTimeSpan()
    {
        var past = DateTime.UtcNow.AddSeconds(-5);

        var elapsed = TimeUtils.Since(past);

        elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(4));
    }

    [Fact]
    public void HasExpired_PastDate_ShouldBeTrue()
    {
        var past = DateTime.UtcNow.AddSeconds(-10);

        TimeUtils.HasExpired(past).Should().BeTrue();
    }

    [Fact]
    public void HasExpired_FutureDate_ShouldBeFalse()
    {
        var future = DateTime.UtcNow.AddHours(1);

        TimeUtils.HasExpired(future).Should().BeFalse();
    }
}

public class GameConstantsTests
{
    [Fact]
    public void MaxPlayerLevel_ShouldBe100()
    {
        GameConstants.MaxPlayerLevel.Should().Be(100);
    }

    [Fact]
    public void MaxFleetSize_ShouldBe256()
    {
        GameConstants.MaxFleetSize.Should().Be(256);
    }

    [Fact]
    public void MaxGuildMembers_ShouldBe500()
    {
        GameConstants.MaxGuildMembers.Should().Be(500);
    }

    [Fact]
    public void MaxInventorySlots_ShouldBe200()
    {
        GameConstants.MaxInventorySlots.Should().Be(200);
    }

    [Fact]
    public void BaseHealth_ShouldBe100()
    {
        GameConstants.BaseHealth.Should().Be(100.0);
    }

    [Fact]
    public void HealthPerLevel_ShouldBe20()
    {
        GameConstants.HealthPerLevel.Should().Be(20.0);
    }

    [Fact]
    public void BaseXpPerLevel_ShouldBe1000()
    {
        GameConstants.BaseXpPerLevel.Should().Be(1000);
    }
}
