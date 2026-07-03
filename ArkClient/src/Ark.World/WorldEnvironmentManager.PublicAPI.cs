using Godot;
using System.Collections.Generic;
using Ark.Abstractions;
using Ark.Events;
using Ark.World.Core;
using Ark.World.Data;

namespace Ark.World;

public sealed partial class WorldEnvironmentManager
{
    public float SampleTerrainHeight(float worldX, float worldZ)
        => _chunkManager?.SampleHeight(worldX, worldZ) ?? 0;

    public void ModifyTerrain(Vector3 pos, float radius, TerrainModType modType, float intensity)
    {
        _chunkManager?.ApplyTerrainModification(pos, radius, modType, intensity);
        _eventBus?.Publish(new TerrainModifiedEvent(pos.X, pos.Y, pos.Z, radius, modType, intensity));
    }

    public void FlattenArea(float centerX, float centerZ, float halfSizeX, float halfSizeZ, float targetHeight)
    {
        float radius = MathF.Max(halfSizeX, halfSizeZ);
        var pos = new Vector3(centerX, targetHeight, centerZ);
        _chunkManager?.ApplyTerrainModification(pos, radius, TerrainModType.Flatten, 1f);
    }

    float ITerrainQuery.SampleHeight(float worldX, float worldZ) => SampleTerrainHeight(worldX, worldZ);

    public void AddAnchoredRegion(float worldX, float worldZ, int chunkRadius = 3)
        => _chunkManager?.AddAnchoredRegion(worldX, worldZ, chunkRadius);

    public void RemoveAnchoredRegion(float worldX, float worldZ)
        => _chunkManager?.RemoveAnchoredRegion(worldX, worldZ);

    public void UpdateAnchoredRegion(int index, float worldX, float worldZ)
        => _chunkManager?.UpdateAnchoredRegion(index, worldX, worldZ);

    public void ClearAnchoredRegions()
        => _chunkManager?.ClearAnchoredRegions();

    public void SetWeather(WeatherType type) => _weather?.ForceWeather(type);

    public void ApplyServerTerrainModifications(IReadOnlyList<PersistedTerrainModification> modifications)
        => _chunkManager?.ApplyPersistedModificationBatch(modifications);

    public float SwitchEnvironment(EnvironmentPreset preset, Vector3 playerPos)
    {
        if (!_initialized || _biomeSampler == null || _chunkManager == null) return playerPos.Y;
        if (preset == _currentPreset) return playerPos.Y;

        _currentPreset = preset;
        GD.Print($"[WorldEnvManager] Switching environment → {preset}");

        _biomeSampler.Override = preset switch
        {
            EnvironmentPreset.BeautifulWild => BiomeId.Plains,
            EnvironmentPreset.DarkForest => BiomeId.Forest,
            EnvironmentPreset.HorrorDungeon => BiomeId.Cave,
            EnvironmentPreset.ModernCity => BiomeId.City,
            EnvironmentPreset.RuinArchaeology => BiomeId.Ruins,
            EnvironmentPreset.MysticSky => BiomeId.SkyIsland,
            EnvironmentPreset.SpaceUniverse => BiomeId.Space,
            _ => null,
        };

        _modLog.Clear();
        _chunkManager.Shutdown();
        _farTerrain?.Shutdown();
        _skyDome?.Shutdown();
        _planet?.Shutdown();
        _chunkManager.Initialize(_seed);
        _skyDome?.Initialize();
        _planet?.Initialize();
        _chunkManager.UpdatePlayerPosition(playerPos);

        ApplyPresetAtmosphere(preset);
        _decorator?.SwitchPreset(preset);

        float safeY = SampleTerrainHeight(playerPos.X, playerPos.Z) + WorldConstants.SpawnHeightMargin;
        _eventBus?.Publish(new EnvironmentSwitchedEvent(preset));
        GD.Print($"[WorldEnvManager] Environment switched to {preset}, safeY={safeY:F1}");
        return safeY;
    }

    private void ApplyPresetAtmosphere(EnvironmentPreset preset)
    {
        switch (preset)
        {
            case EnvironmentPreset.HorrorDungeon:
                _atmosphere?.ApplyModeOverride("Dungeon", new Color(0.15f, 0.15f, 0.18f), new Color(0.45f, 0.45f, 0.5f), 0.8f, new Color(0.6f, 0.6f, 0.65f), 0.7f);
                SetTimeOfDay(0.0f);
                break;
            case EnvironmentPreset.DarkForest:
                _atmosphere?.ApplyModeOverride("DarkForest", new Color(0.05f, 0.08f, 0.04f), new Color(0.15f, 0.2f, 0.1f), 0.3f, new Color(0.5f, 0.6f, 0.4f), 0.6f);
                SetTimeOfDay(0.85f);
                break;
            case EnvironmentPreset.MysticSky:
                _atmosphere?.ApplyModeOverride("Sky", new Color(0.6f, 0.75f, 1f), new Color(0.4f, 0.5f, 0.8f), 0.6f, new Color(1f, 0.95f, 0.85f), 1.2f);
                SetTimeOfDay(0.35f);
                break;
            case EnvironmentPreset.SpaceUniverse:
                _atmosphere?.ApplyModeOverride("Space", new Color(0.02f, 0.02f, 0.06f), new Color(0.15f, 0.15f, 0.25f), 0.3f, new Color(0.9f, 0.9f, 1f), 1.5f);
                break;
            case EnvironmentPreset.ModernCity:
                _atmosphere?.ApplyModeOverride("City", new Color(0.4f, 0.42f, 0.5f), new Color(0.3f, 0.3f, 0.35f), 0.5f, new Color(0.9f, 0.85f, 0.8f), 1f);
                SetTimeOfDay(0.8f);
                break;
            case EnvironmentPreset.RuinArchaeology:
                _atmosphere?.ApplyModeOverride("Ruins", new Color(0.55f, 0.48f, 0.35f), new Color(0.4f, 0.35f, 0.25f), 0.4f, new Color(1f, 0.9f, 0.7f), 1.1f);
                SetTimeOfDay(0.5f);
                break;
            default:
                _atmosphere?.ClearModeOverride();
                SetTimeOfDay(0.3f);
                break;
        }
    }

    public void SetTimeOfDay(float normalized)
    {
        _timeState.NormalizedTimeOfDay = MathF.Max(0, MathF.Min(normalized, 0.9999f));
    }

    public void SetPaused(bool paused) => _paused = paused;

    /// <summary>
    /// 用服务端种子重新初始化世界（如果当前种子不同或尚未初始化）。
    /// 先关闭旧世界再以新种子重建，确保地形/天气/生态使用服务端权威数据。
    /// </summary>
    public void ReinitializeWithSeed(long terrainSeed)
    {
        GD.Print($"[WorldEnvManager] ✅ ReinitializeWithSeed called with server seed={terrainSeed}");
        _serverSeedProvided = true;

        var newSeed = new WorldSeed(terrainSeed);

        if (_initialized && _seed.Value == newSeed.Value)
        {
            GD.Print($"[WorldEnvManager] Seed unchanged ({terrainSeed}), skip reinit");
            return;
        }

        if (_initialized)
        {
            GD.Print($"[WorldEnvManager] Reinitializing: old={_seed.Value}, new={terrainSeed}");
            ShutdownWorld(); // sets _initialized = false
        }

        InitializeWorld(newSeed, _eventBus);
    }

    /// <summary>
    /// 应用服务端时间（归一化 0-1）。
    /// </summary>
    public void ApplyServerTimeOfDay(float timeOfDay)
    {
        // 服务端时间 0-24 → 归一化 0-1
        var normalized = timeOfDay / 24f;
        SetTimeOfDay(MathF.Max(0, MathF.Min(normalized, 0.9999f)));
    }

    public void ApplyModeOverride(GameplayMode mode)
    {
        switch (mode)
        {
            case GameplayMode.Space:
                _atmosphere?.ApplyModeOverride("Space", new Color(0.02f, 0.02f, 0.06f), new Color(0.15f, 0.15f, 0.25f), 0.3f, new Color(0.9f, 0.9f, 1f), 1.5f);
                SetPaused(true);
                break;
            default:
                _atmosphere?.ClearModeOverride();
                SetPaused(false);
                break;
        }
    }
}
