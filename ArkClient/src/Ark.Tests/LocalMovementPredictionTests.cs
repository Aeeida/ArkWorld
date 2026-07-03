using Xunit;
using Ark.Ecs.Components;
using Ark.Systems.LocalControl;

namespace Ark.Tests;

public class LocalMovementPredictionTests
{
    private static LocalMovementPredict BasePredict() => new()
    {
        WalkSpeed = 4f,
        SprintSpeed = 8f,
        Acceleration = 40f,
        Deceleration = 30f,
        AirControl = 0.5f,
        Gravity = 20f,
        JumpVelocity = 7f,
        MaxJumps = 2,
        JumpsRemaining = 2,
        IsOnFloor = 1,
    };

    [Fact]
    public void NoInput_ZeroVelocity_StaysZero()
    {
        var p = BasePredict();
        var i = new InputIntent { HasIntent = 1 };
        var r = LocalMovementPredictionSystem.Step(p, i, 0.016f);
        Assert.Equal(0f, r.VelocityX);
        Assert.Equal(0f, r.VelocityZ);
        Assert.Equal(0f, r.VelocityY);
    }

    [Fact]
    public void ForwardInput_AcceleratesTowardWalkSpeed()
    {
        var p = BasePredict();
        var i = new InputIntent { HasIntent = 1, MoveZ = 1f };
        // 0.1s at 40 accel: should reach 4 (walk speed) in 0.1s
        var r = LocalMovementPredictionSystem.Step(p, i, 0.1f);
        Assert.Equal(0f, r.VelocityX);
        Assert.Equal(4f, r.VelocityZ);
    }

    [Fact]
    public void Sprint_AcceleratesTowardSprintSpeed()
    {
        var p = BasePredict();
        var i = new InputIntent { HasIntent = 1, MoveZ = 1f, SprintHeld = 1 };
        // need 0.2s at 40 accel to reach 8
        var r = LocalMovementPredictionSystem.Step(p, i, 0.2f);
        Assert.Equal(8f, r.VelocityZ);
    }

    [Fact]
    public void Gravity_AppliesWhenAirborne()
    {
        var p = BasePredict();
        p.IsOnFloor = 0;
        p.JumpsRemaining = 1;
        var i = new InputIntent { HasIntent = 1 };
        var r = LocalMovementPredictionSystem.Step(p, i, 0.5f);
        Assert.Equal(-10f, r.VelocityY);
        Assert.Equal(1, r.JumpsRemaining);
    }

    [Fact]
    public void JumpJustPressed_ConsumesJump()
    {
        var p = BasePredict();
        var i = new InputIntent { HasIntent = 1, JumpJustPressed = 1 };
        var r = LocalMovementPredictionSystem.Step(p, i, 0.016f);
        Assert.Equal(7f, r.VelocityY);
        Assert.Equal(1, r.JumpsRemaining);
        Assert.Equal((byte)1, r.JumpRequested);
    }

    [Fact]
    public void DiagonalInput_NormalizedToTargetSpeed()
    {
        var p = BasePredict();
        var i = new InputIntent { HasIntent = 1, MoveX = 1f, MoveZ = 1f };
        // Step long enough to fully reach target
        var r = LocalMovementPredictionSystem.Step(p, i, 1f);
        var mag = System.MathF.Sqrt(r.VelocityX * r.VelocityX + r.VelocityZ * r.VelocityZ);
        Assert.True(System.MathF.Abs(mag - 4f) < 0.01f, $"Expected magnitude ~4, got {mag}");
    }

    [Fact]
    public void Suppressed_NoInputApplied()
    {
        var p = BasePredict();
        p.VelocityX = 3f; p.VelocityZ = 3f;
        var i = new InputIntent { HasIntent = 0, MoveZ = 1f, SprintHeld = 1, JumpJustPressed = 1 };
        var r = LocalMovementPredictionSystem.Step(p, i, 0.1f);
        // suppressed: HasIntent=0 ⇒ hasInput=false, no jump consumed
        Assert.Equal(2, r.JumpsRemaining);
        Assert.Equal(0f, r.VelocityY);
    }
}

public class CameraOrbitTests
{
    private static CameraOrbitState BaseOrbit() => new()
    {
        Yaw = 0f, Pitch = 0f, Zoom = 5f,
        TargetYaw = 0f, TargetPitch = 0f, TargetZoom = 5f,
        MinPitch = -1f, MaxPitch = 1f,
        MinZoom = 2f, MaxZoom = 10f,
        YawSensitivity = 0.01f,
        PitchSensitivity = 0.01f,
        ZoomSensitivity = 1f,
        SmoothFactor = 50f, // ~instant within 1 frame
    };

    [Fact]
    public void MouseDelta_AccumulatesYaw()
    {
        var o = BaseOrbit();
        var i = new InputIntent { HasIntent = 1, AimDeltaX = 100f };
        var r = CameraOrbitSystem.Step(o, i, 1f);
        Assert.Equal(-1f, r.TargetYaw);
        Assert.True(r.Yaw < 0f);
    }

    [Fact]
    public void Pitch_ClampedWithinBounds()
    {
        var o = BaseOrbit();
        var i = new InputIntent { HasIntent = 1, AimDeltaY = -10000f }; // huge upward
        var r = CameraOrbitSystem.Step(o, i, 1f);
        Assert.True(r.TargetPitch <= 1.0001f);
        Assert.True(r.TargetPitch >= -1.0001f);
    }

    [Fact]
    public void NoIntent_NoChange()
    {
        var o = BaseOrbit();
        o.Yaw = 0.5f; o.TargetYaw = 0.5f;
        var i = new InputIntent { HasIntent = 0, AimDeltaX = 10000f };
        var r = CameraOrbitSystem.Step(o, i, 0.016f);
        Assert.Equal(0.5f, r.TargetYaw);
    }

    [Fact]
    public void Zoom_ClampedAndSmooths()
    {
        var o = BaseOrbit();
        var i = new InputIntent { HasIntent = 1, ZoomDelta = -100f }; // wheel forward to zoom in
        var r = CameraOrbitSystem.Step(o, i, 1f);
        Assert.True(r.TargetZoom >= 2f);
        Assert.True(r.TargetZoom <= 10f);
    }
}
