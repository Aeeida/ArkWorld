using Xunit;
using Ark.Player.Vehicle;

namespace Ark.Tests;

public class VehicleDriveInputTests
{
    [Fact]
    public void TurnRate_AtZeroSpeed_ReturnsMinimum()
    {
        float rate = VehicleDriveInput.TurnRate(0f, 100f);
        Assert.Equal(2f * 0.3f, rate, 0.01f);
    }

    [Fact]
    public void TurnRate_AtMaxSpeed_ReturnsMaximum()
    {
        float rate = VehicleDriveInput.TurnRate(100f, 100f);
        Assert.Equal(2f, rate, 0.01f);
    }

    [Fact]
    public void TargetSpeed_ForwardSprinting_ReturnsMaxSpeed()
    {
        float spd = VehicleDriveInput.TargetSpeed(1f, 100f, true);
        Assert.Equal(100f, spd, 0.01f);
    }

    [Fact]
    public void TargetSpeed_ForwardNotSprinting_Returns70Percent()
    {
        float spd = VehicleDriveInput.TargetSpeed(1f, 100f, false);
        Assert.Equal(70f, spd, 0.01f);
    }

    [Fact]
    public void TargetSpeed_Reverse_Returns80Percent()
    {
        float spd = VehicleDriveInput.TargetSpeed(-1f, 100f, false);
        Assert.Equal(-80f, spd, 0.01f);
    }

    [Fact]
    public void ApproachSpeed_MovesTowardTarget()
    {
        float result = VehicleDriveInput.ApproachSpeed(0f, 100f, 100f, 0.1f);
        Assert.True(result > 0f);
        Assert.True(result < 100f);
    }

    [Fact]
    public void ApproachSpeed_SnapsWhenClose()
    {
        float result = VehicleDriveInput.ApproachSpeed(99.9f, 100f, 100f, 1f);
        Assert.Equal(100f, result);
    }
}
