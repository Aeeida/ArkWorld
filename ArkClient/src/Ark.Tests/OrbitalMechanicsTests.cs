using Xunit;
using Ark.Configuration;
using Ark.Gameplay.Space;
using Ark.Shared.Data;

namespace Ark.Tests;

public class OrbitalMechanicsTests
{
    [Fact]
    public void CircularSpeed_AtSurface_ReasonableValue()
    {
        float v = OrbitalMechanics.CircularSpeed(0);
        // Earth surface: ~7905 m/s
        Assert.InRange(v, 7800f, 8000f);
    }

    [Fact]
    public void CircularSpeed_Higher_IsSlower()
    {
        float vLow = OrbitalMechanics.CircularSpeed(200_000);
        float vHigh = OrbitalMechanics.CircularSpeed(400_000);
        Assert.True(vHigh < vLow);
    }

    [Fact]
    public void TsiolkovskyDeltaV_ReturnsPositive()
    {
        float dv = OrbitalMechanics.TsiolkovskyDeltaV(320f, 10000f, 3000f);
        Assert.True(dv > 0);
    }

    [Fact]
    public void TsiolkovskyDeltaV_ZeroDryMass_ReturnsZero()
    {
        float dv = OrbitalMechanics.TsiolkovskyDeltaV(320f, 10000f, 0f);
        Assert.Equal(0f, dv);
    }

    [Fact]
    public void TsiolkovskyDeltaV_KnownValue()
    {
        // ISP=300s, mass ratio=e ≈ 2.718 → ΔV = 300 * 9.81 * ln(e) = 300 * 9.81 ≈ 2943
        float e = MathF.E;
        float dv = OrbitalMechanics.TsiolkovskyDeltaV(300f, e * 1000f, 1000f);
        Assert.InRange(dv, 2900f, 3000f);
    }

    [Fact]
    public void FromApsides_CircularOrbit_EccentricityZero()
    {
        var p = OrbitalMechanics.FromApsides(200_000, 200_000);
        Assert.Equal(0f, p.Eccentricity, 0.001f);
    }

    [Fact]
    public void FromApsides_EllipticalOrbit_HasEccentricity()
    {
        var p = OrbitalMechanics.FromApsides(400_000, 200_000);
        Assert.True(p.Eccentricity > 0);
        Assert.True(p.Eccentricity < 1);
    }

    [Fact]
    public void IsStableOrbit_CircularAboveMin_True()
    {
        var p = OrbitalMechanics.FromApsides(200_000, 200_000);
        Assert.True(OrbitalMechanics.IsStableOrbit(p));
    }

    [Fact]
    public void IsStableOrbit_BelowMinAltitude_False()
    {
        var p = OrbitalMechanics.FromApsides(200_000, 100_000); // periapsis below min
        Assert.False(OrbitalMechanics.IsStableOrbit(p));
    }

    [Fact]
    public void GravityAt_Surface_Approx9_8()
    {
        float g = OrbitalMechanics.GravityAt(0);
        Assert.InRange(g, 9.7f, 9.9f);
    }

    [Fact]
    public void GravityAt_Higher_IsWeaker()
    {
        float gLow = OrbitalMechanics.GravityAt(0);
        float gHigh = OrbitalMechanics.GravityAt(1_000_000);
        Assert.True(gHigh < gLow);
    }

    [Fact]
    public void AtmosphereDensity_AtSurface_IsOne()
    {
        Assert.Equal(1f, OrbitalMechanics.AtmosphereDensity(0));
    }

    [Fact]
    public void AtmosphereDensity_AboveAtmosphere_IsZero()
    {
        Assert.Equal(0f, OrbitalMechanics.AtmosphereDensity(100_001));
    }

    [Fact]
    public void CircularizationDeltaV_CircularOrbit_Zero()
    {
        // Same apo/peri → already circular → ΔV ≈ 0
        float dv = OrbitalMechanics.CircularizationDeltaV(200_000, 200_000);
        Assert.InRange(dv, 0f, 0.1f);
    }
}
