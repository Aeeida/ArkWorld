using Godot;
using Ark.Events;
using Ark.World.Core;

namespace Ark.World;

public sealed partial class WorldEnvironmentManager
{
    private void OnExplosion(ExplosionEvent e)
    {
        ModifyTerrain(new Vector3(e.PosX, e.PosY, e.PosZ), e.Radius, TerrainModType.Explosion, e.Radius * 2f);
    }

    public void ShutdownWorld()
    {
        _chunkManager?.Shutdown();
        _farTerrain?.Shutdown();
        _skyDome?.Shutdown();
        _planet?.Shutdown();
        _dayNight?.Shutdown();
        _weather?.Shutdown();
        _atmosphere?.Shutdown();
        _precipitation?.Shutdown();
        _vegetation?.Shutdown();
        _decorator?.Shutdown();
        _initialized = false;
        GD.Print("[WorldEnvManager] Shutdown");
    }

    public override void _ExitTree()
    {
        ShutdownWorld();
    }
}
