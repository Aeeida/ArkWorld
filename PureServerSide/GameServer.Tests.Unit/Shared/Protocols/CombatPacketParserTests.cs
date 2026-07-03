using FluentAssertions;
using Game.Shared.Protocols.Serialization;

namespace GameServer.Tests.Unit.Shared.Protocols;

public class CombatPacketParserTests
{
    [Fact]
    public void DamagePacket_WriteAndParse_Roundtrip()
    {
        var attackerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var damage = 42.5;

        Span<byte> buffer = stackalloc byte[64];
        var written = CombatPacketParser.WriteDamagePacket(buffer, attackerId, targetId, damage);

        written.Should().BeGreaterThan(0);

        var success = CombatPacketParser.TryParseDamagePacket(buffer[..written], out var parsedAttacker, out var parsedTarget, out var parsedDamage);

        success.Should().BeTrue();
        parsedAttacker.Should().Be(attackerId);
        parsedTarget.Should().Be(targetId);
        parsedDamage.Should().Be(damage);
    }

    [Fact]
    public void DamagePacket_TooSmallBuffer_ShouldReturnZero()
    {
        Span<byte> buffer = stackalloc byte[5];
        var written = CombatPacketParser.WriteDamagePacket(buffer, Guid.NewGuid(), Guid.NewGuid(), 10.0);

        written.Should().Be(0);
    }

    [Fact]
    public void DamagePacket_ParseTooSmall_ShouldReturnFalse()
    {
        Span<byte> data = stackalloc byte[5];
        data[0] = CombatPacketParser.DamagePacketId;

        var success = CombatPacketParser.TryParseDamagePacket(data, out _, out _, out _);

        success.Should().BeFalse();
    }

    [Fact]
    public void DamagePacket_WrongPacketId_ShouldReturnFalse()
    {
        Span<byte> buffer = stackalloc byte[64];
        CombatPacketParser.WriteDamagePacket(buffer, Guid.NewGuid(), Guid.NewGuid(), 10.0);
        buffer[0] = 0xFF; // corrupt packet id

        var success = CombatPacketParser.TryParseDamagePacket(buffer, out _, out _, out _);

        success.Should().BeFalse();
    }

    [Fact]
    public void DamagePacket_ZeroDamage_ShouldRoundtrip()
    {
        var attackerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        Span<byte> buffer = stackalloc byte[64];
        var written = CombatPacketParser.WriteDamagePacket(buffer, attackerId, targetId, 0.0);

        var success = CombatPacketParser.TryParseDamagePacket(buffer[..written], out _, out _, out var damage);

        success.Should().BeTrue();
        damage.Should().Be(0.0);
    }

    [Fact]
    public void MovePacket_WriteAndParse_Roundtrip()
    {
        var entityId = Guid.NewGuid();
        double x = 1.0, y = 2.0, z = 3.0;
        float rotation = 45f;

        Span<byte> buffer = stackalloc byte[64];
        var written = CombatPacketParser.WriteMovePacket(buffer, entityId, x, y, z, rotation);

        written.Should().BeGreaterThan(0);

        var success = CombatPacketParser.TryParseMovePacket(buffer[..written], out var eid, out var px, out var py, out var pz, out var pr);

        success.Should().BeTrue();
        eid.Should().Be(entityId);
        px.Should().Be(x);
        py.Should().Be(y);
        pz.Should().Be(z);
        pr.Should().Be(rotation);
    }

    [Fact]
    public void MovePacket_TooSmallBuffer_ShouldReturnZero()
    {
        Span<byte> buffer = stackalloc byte[5];
        var written = CombatPacketParser.WriteMovePacket(buffer, Guid.NewGuid(), 1, 2, 3, 0f);

        written.Should().Be(0);
    }

    [Fact]
    public void MovePacket_ParseTooSmall_ShouldReturnFalse()
    {
        Span<byte> data = stackalloc byte[5];
        data[0] = CombatPacketParser.MovePacketId;

        var success = CombatPacketParser.TryParseMovePacket(data, out _, out _, out _, out _, out _);

        success.Should().BeFalse();
    }

    [Fact]
    public void MovePacket_WrongPacketId_ShouldReturnFalse()
    {
        Span<byte> buffer = stackalloc byte[64];
        CombatPacketParser.WriteMovePacket(buffer, Guid.NewGuid(), 1, 2, 3, 0f);
        buffer[0] = 0xFF;

        var success = CombatPacketParser.TryParseMovePacket(buffer, out _, out _, out _, out _, out _);

        success.Should().BeFalse();
    }

    [Fact]
    public void MovePacket_NegativeCoordinates_ShouldRoundtrip()
    {
        var entityId = Guid.NewGuid();

        Span<byte> buffer = stackalloc byte[64];
        var written = CombatPacketParser.WriteMovePacket(buffer, entityId, -100.5, -200.5, -300.5, -45f);

        var success = CombatPacketParser.TryParseMovePacket(buffer[..written], out var eid, out var px, out var py, out var pz, out var pr);

        success.Should().BeTrue();
        px.Should().Be(-100.5);
        py.Should().Be(-200.5);
        pz.Should().Be(-300.5);
        pr.Should().Be(-45f);
    }
}
