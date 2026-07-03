using Xunit;
using Ark.World.Core;
using Ark.World.Data;

namespace Ark.Tests;

public class WorldTimeStateTests
{
    [Fact]
    public void Default_StartsInMorning()
    {
        var state = new WorldTimeState();
        Assert.Equal(DayPeriod.Morning, state.Period);
    }

    [Fact]
    public void Default_StartsInSpring()
    {
        var state = new WorldTimeState();
        Assert.Equal(Season.Spring, state.CurrentSeason);
    }

    [Fact]
    public void Default_DayZero()
    {
        var state = new WorldTimeState();
        Assert.Equal(0, state.Day);
    }

    [Fact]
    public void NormalizedTimeOfDay_InitiallyMorning()
    {
        var state = new WorldTimeState();
        // 0.3 = morning time
        Assert.InRange(state.NormalizedTimeOfDay, 0.25f, 0.5f);
    }

    [Fact]
    public void DaysPerSeason_Default15()
    {
        var state = new WorldTimeState();
        Assert.Equal(15, state.DaysPerSeason);
    }

    [Fact]
    public void MoonPhase_InitiallyZero()
    {
        var state = new WorldTimeState();
        Assert.Equal(0f, state.MoonPhase);
    }

    [Fact]
    public void TotalElapsed_InitiallyZero()
    {
        var state = new WorldTimeState();
        Assert.Equal(0.0, state.TotalElapsed);
    }

    // ═══ WorldConstants validation ═══

    [Fact]
    public void DayDuration_IsReasonable()
    {
        // Should be between 1 minute and 1 hour
        Assert.InRange(WorldConstants.DayDurationSeconds, 60f, 3600f);
    }

    [Fact]
    public void Sunrise_BeforeNoon()
    {
        Assert.True(WorldConstants.SunriseNormalized < 0.5f);
    }

    [Fact]
    public void Sunset_AfterNoon()
    {
        Assert.True(WorldConstants.SunsetNormalized > 0.5f);
    }

    [Fact]
    public void Sunrise_BeforeSunset()
    {
        Assert.True(WorldConstants.SunriseNormalized < WorldConstants.SunsetNormalized);
    }

    [Fact]
    public void ChunkSize_Positive()
    {
        Assert.True(WorldConstants.ChunkSize > 0);
    }

    [Fact]
    public void HeightmapResolution_OddValue()
    {
        // Resolution should be 2^n + 1 for proper LOD
        Assert.Equal(1, WorldConstants.HeightmapResolution % 2);
    }

    [Fact]
    public void LoadRadius_LessThanUnloadRadius()
    {
        Assert.True(WorldConstants.LoadRadius < WorldConstants.UnloadRadius);
    }

    [Fact]
    public void HighLodRadius_LessThanLoadRadius()
    {
        Assert.True(WorldConstants.HighLodRadius < WorldConstants.LoadRadius);
    }

    // ═══ Enum coverage ═══

    [Fact]
    public void DayPeriod_HasExpectedValues()
    {
        var values = Enum.GetValues<DayPeriod>();
        Assert.Contains(DayPeriod.Dawn, values);
        Assert.Contains(DayPeriod.Morning, values);
        Assert.Contains(DayPeriod.Noon, values);
        Assert.Contains(DayPeriod.Afternoon, values);
        Assert.Contains(DayPeriod.Dusk, values);
        Assert.Contains(DayPeriod.Night, values);
        Assert.Contains(DayPeriod.Midnight, values);
    }

    [Fact]
    public void Season_HasFourSeasons()
    {
        var values = Enum.GetValues<Season>();
        Assert.Equal(4, values.Length);
    }

    [Fact]
    public void WeatherType_HasMultipleTypes()
    {
        var values = Enum.GetValues<WeatherType>();
        Assert.True(values.Length >= 8);
    }

    [Fact]
    public void TerrainModType_HasExpectedValues()
    {
        var values = Enum.GetValues<TerrainModType>();
        Assert.Contains(TerrainModType.Dig, values);
        Assert.Contains(TerrainModType.Fill, values);
        Assert.Contains(TerrainModType.Flatten, values);
        Assert.Contains(TerrainModType.Explosion, values);
        Assert.Contains(TerrainModType.Erosion, values);
    }
}
