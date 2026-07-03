using Godot;

namespace Ark.Player;

public partial class TpsPlayerController
{
    // ═══════════════════════════════════════════════════════════════════════
    //                          相机偏移 / 缩放
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 相机偏移平滑过渡 + 缩放 + 地形穿透保护。
    /// </summary>
    private void UpdateCameraOffset(float dt)
    {
        if (_camera == null) return;

        var targetOffset = _buildCameraMode
            ? new Vector3(0, BuildCameraOffset.Y, _currentZoom)
            : new Vector3(0, TpsCameraOffset.Y, _currentZoom);

        float lerpFactor = 7f * dt;
        _camera.Position = _camera.Position.Lerp(targetOffset, Mathf.Clamp(lerpFactor, 0f, 1f));

        // ── 地形穿透保护：相机不得低于地形表面 ──
        if (_sampleTerrainHeight != null)
        {
            var camGlobalPos = _camera.GlobalPosition;
            float terrainY = _sampleTerrainHeight(camGlobalPos.X, camGlobalPos.Z);
            if (camGlobalPos.Y < terrainY + 0.5f)
            {
                float correction = terrainY + 0.5f - camGlobalPos.Y;
                _camera.Position = new Vector3(_camera.Position.X, _camera.Position.Y + correction, _camera.Position.Z);
            }
        }

        // ── 动态远裁面：高空时扩展以看到星球 ──
        float altitude = _camera.GlobalPosition.Y;
        _camera.Far = altitude > 2000f ? 200000f : altitude > 500f ? 50000f : 5000f;

        // ── 准心偏移随缩放距离调整 ──
        _crosshairWidget?.SetZoomDistance(_currentZoom);
    }

    /// <summary>
    /// 载具模式专用相机偏移 — 相机直接在全局空间中跟踪载具锚点。
    /// _cameraRig 已被直接定位到 _vehicleCameraAnchor，此方法设定相机到锚点的距离。
    /// </summary>
    private void UpdateVehicleCameraOffset(float dt)
    {
        if (_camera == null) return;

        var targetOffset = new Vector3(0, TpsCameraOffset.Y, _currentZoom);
        _camera.Position = _camera.Position.Lerp(targetOffset, Mathf.Clamp(30f * dt, 0f, 1f));

        // ── 地形穿透保护 ──
        if (_sampleTerrainHeight != null)
        {
            var camGlobalPos = _camera.GlobalPosition;
            float terrainY = _sampleTerrainHeight(camGlobalPos.X, camGlobalPos.Z);
            if (camGlobalPos.Y < terrainY + 0.5f)
            {
                float correction = terrainY + 0.5f - camGlobalPos.Y;
                _camera.Position = new Vector3(_camera.Position.X, _camera.Position.Y + correction, _camera.Position.Z);
            }
        }

        float altitude = _camera.GlobalPosition.Y;
        _camera.Far = altitude > 2000f ? 200000f : altitude > 500f ? 50000f : 5000f;

        _crosshairWidget?.SetZoomDistance(_currentZoom);
    }
}
