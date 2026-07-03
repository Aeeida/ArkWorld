using Godot;
using Ark.World.Core;
using Ark.World.Data;

namespace Ark.World;

public sealed partial class WorldEnvironmentManager
{
    public override void _Process(double delta)
    {
        if (!_initialized || _paused) return;
        float dt = (float)delta;

        _dayNight?.Update(dt);
        _weather?.Update(dt);
        _atmosphere?.Update(dt);
        _precipitation?.Update(dt);
        _vegetation?.Update(dt);
        _chunkManager?.Update(dt);
    }

    public void UpdateCameraPosition(Vector3 cameraPos, float cameraAltitude)
    {
        if (!_initialized) return;

        _chunkManager?.UpdateCameraPosition(cameraPos, cameraAltitude);
        _farTerrain?.Update(cameraPos.X, cameraPos.Z, cameraAltitude);
        _precipitation?.UpdateFollowPosition(cameraPos);
        _decorator?.UpdateFollowPosition(cameraPos);

        if (_skyDome != null)
        {
            if (_dayNight != null)
            {
                var skyColor = _dayNight.GetSkyColor();
                _skyDome.SkyTopColor = skyColor;
                _skyDome.HorizonColor = skyColor.Lerp(Colors.White, 0.35f);
                _skyDome.SunDirection = _dayNight.GetSunDirection();
            }
            float spaceBlend = cameraAltitude < WorldConstants.PlanetVisibleMinAlt ? 0f
                : cameraAltitude > WorldConstants.SpaceAltitudeThreshold ? 1f
                : (cameraAltitude - WorldConstants.PlanetVisibleMinAlt) /
                  (WorldConstants.SpaceAltitudeThreshold - WorldConstants.PlanetVisibleMinAlt);
            _skyDome.SpaceMode = spaceBlend > 0.01f;
            _skyDome.SpaceBlend = spaceBlend;
            _skyDome.Update(cameraPos, cameraAltitude);
        }

        _planet?.Update(cameraAltitude);

        var biome = _chunkManager?.GetBiomeAt(cameraPos.X, cameraPos.Z) ?? BiomeId.None;
        if (biome != _playerBiome && biome.IsValid)
        {
            var prev = _playerBiome;
            _playerBiome = biome;
            _weather?.SetCurrentBiome(biome);
            _eventBus?.Publish(new BiomeEnteredEvent(biome, prev));
        }
    }

    public void UpdatePlayerPosition(Vector3 worldPos)
    {
        UpdateCameraPosition(worldPos, worldPos.Y);
    }
}
