using Xunit;
using Ark.World.Core;
using Ark.World.Data;

namespace Ark.Tests;

public class WeatherStateTests
{
    [Fact]
    public void Default_IsClear()
    {
        var state = new WeatherState();
        Assert.Equal(WeatherType.Clear, state.CurrentType);
    }

    [Fact]
    public void Default_TransitionComplete()
    {
        var state = new WeatherState();
        Assert.Equal(1f, state.TransitionProgress);
    }

    [Theory]
    [InlineData(WeatherType.Clear)]
    [InlineData(WeatherType.Cloudy)]
    [InlineData(WeatherType.LightRain)]
    [InlineData(WeatherType.HeavyRain)]
    [InlineData(WeatherType.Thunderstorm)]
    [InlineData(WeatherType.Fog)]
    [InlineData(WeatherType.Snow)]
    [InlineData(WeatherType.Sandstorm)]
    public void FromType_SetsCurrentType(WeatherType type)
    {
        var state = WeatherState.FromType(type);
        Assert.Equal(type, state.CurrentType);
        Assert.Equal(type, state.TargetType);
        Assert.Equal(1f, state.TransitionProgress);
    }

    [Fact]
    public void FromType_Clear_LowCloudCoverage()
    {
        var state = WeatherState.FromType(WeatherType.Clear);
        Assert.True(state.CloudCoverage < 0.3f);
    }

    [Fact]
    public void FromType_Thunderstorm_HighCloudsAndRain()
    {
        var state = WeatherState.FromType(WeatherType.Thunderstorm);
        Assert.True(state.CloudCoverage >= 0.9f);
        Assert.True(state.PrecipitationIntensity >= 0.8f);
        Assert.True(state.LightningFrequency > 0f);
    }

    [Fact]
    public void FromType_Snow_HasPrecipitation()
    {
        var state = WeatherState.FromType(WeatherType.Snow);
        Assert.True(state.PrecipitationIntensity > 0f);
        Assert.True(state.Temperature < 0f);
    }

    [Fact]
    public void FromType_Fog_HighFogDensity()
    {
        var state = WeatherState.FromType(WeatherType.Fog);
        Assert.True(state.FogDensity >= 0.3f);
    }

    [Fact]
    public void FromType_Sandstorm_HighWindSpeed()
    {
        var state = WeatherState.FromType(WeatherType.Sandstorm);
        Assert.True(state.WindSpeed >= 20f);
        Assert.True(state.FogDensity >= 0.5f); // sand haze
    }

    [Fact]
    public void FromType_HeavyRain_HigherThanLightRain()
    {
        var light = WeatherState.FromType(WeatherType.LightRain);
        var heavy = WeatherState.FromType(WeatherType.HeavyRain);
        Assert.True(heavy.PrecipitationIntensity > light.PrecipitationIntensity);
    }

    [Theory]
    [InlineData(WeatherType.Clear)]
    [InlineData(WeatherType.Cloudy)]
    [InlineData(WeatherType.LightRain)]
    [InlineData(WeatherType.HeavyRain)]
    [InlineData(WeatherType.Thunderstorm)]
    [InlineData(WeatherType.Fog)]
    [InlineData(WeatherType.Snow)]
    [InlineData(WeatherType.Sandstorm)]
    public void FromType_AllTypes_HaveFiniteValues(WeatherType type)
    {
        var state = WeatherState.FromType(type);
        Assert.True(float.IsFinite(state.CloudCoverage));
        Assert.True(float.IsFinite(state.PrecipitationIntensity));
        Assert.True(float.IsFinite(state.WindSpeed));
        Assert.True(float.IsFinite(state.FogDensity));
        Assert.True(float.IsFinite(state.Temperature));
        Assert.True(float.IsFinite(state.Humidity));
        Assert.True(float.IsFinite(state.LightningFrequency));
    }
}
