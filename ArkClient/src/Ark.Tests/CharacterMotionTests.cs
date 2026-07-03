using Xunit;
using Ark.Player.Character;

namespace Ark.Tests;

public class CharacterMotionTests
{
    [Fact]
    public void TargetSpeed_Walking_ReturnsWalkSpeed()
    {
        Assert.Equal(5f, CharacterMotion.TargetSpeed(false, 5f, 10f));
    }

    [Fact]
    public void TargetSpeed_Sprinting_ReturnsSprintSpeed()
    {
        Assert.Equal(10f, CharacterMotion.TargetSpeed(true, 5f, 10f));
    }

    [Fact]
    public void ApplyGravity_InAir_DecreasesVelocity()
    {
        var (vy, jumps) = CharacterMotion.ApplyGravity(0f, 20f, 0.1f, false, 2, 2);
        Assert.Equal(-2f, vy, 0.001f);
        Assert.Equal(2, jumps);
    }

    [Fact]
    public void ApplyGravity_OnFloor_ResetsJumps()
    {
        var (vy, jumps) = CharacterMotion.ApplyGravity(-5f, 20f, 0.1f, true, 0, 2);
        Assert.Equal(0f, vy);
        Assert.Equal(2, jumps);
    }

    [Fact]
    public void TryJump_WithJumpsRemaining_AppliesForce()
    {
        var (vy, jumps) = CharacterMotion.TryJump(0f, 8f, 2, true);
        Assert.Equal(8f, vy);
        Assert.Equal(1, jumps);
    }

    [Fact]
    public void TryJump_NoJumps_DoesNothing()
    {
        var (vy, jumps) = CharacterMotion.TryJump(0f, 8f, 0, true);
        Assert.Equal(0f, vy);
        Assert.Equal(0, jumps);
    }

    [Fact]
    public void ApplyHorizontal_WithInput_Accelerates()
    {
        var (vx, vz) = CharacterMotion.ApplyHorizontal(
            0f, 0f, 5f, 0f, 10f, 15f, 0.3f, true, true, 0.1f);
        Assert.True(vx > 0f);
        Assert.Equal(0f, vz);
    }

    [Fact]
    public void ApplyHorizontal_NoInput_Decelerates()
    {
        var (vx, _) = CharacterMotion.ApplyHorizontal(
            5f, 0f, 0f, 0f, 10f, 15f, 0.3f, true, false, 0.1f);
        Assert.True(vx < 5f);
    }
}
