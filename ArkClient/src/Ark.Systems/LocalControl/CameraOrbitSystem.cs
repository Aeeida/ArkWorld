using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;

namespace Ark.Systems.LocalControl;

/// <summary>
/// 第三人称相机轨道 System (Phase 4)。
/// 读取 <see cref="InputIntent.AimDeltaX/Y"/>+<see cref="InputIntent.ZoomDelta"/>，
/// 累加并夹紧到 <see cref="CameraOrbitState.TargetYaw/Pitch/Zoom"/>，
/// 然后用 SmoothFactor 让当前 Yaw/Pitch/Zoom 平滑趋近 Target。
/// 相机表现层（CameraController/TpsCameraRig）订阅 ECS 输出最终值，避免节点自查询输入。
/// </summary>
public sealed class CameraOrbitSystem
{
    private readonly EntityStore _store;
    private readonly List<Entity> _tracked = new();

    public CameraOrbitSystem(EntityStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public void Track(Entity entity)
    {
        if (entity.IsNull) return;
        for (int i = 0; i < _tracked.Count; i++)
            if (_tracked[i].Id == entity.Id) return;
        _tracked.Add(entity);
    }

    public void Untrack(Entity entity)
    {
        if (entity.IsNull) return;
        for (int i = 0; i < _tracked.Count; i++)
        {
            if (_tracked[i].Id == entity.Id) { _tracked.RemoveAt(i); return; }
        }
    }

    public void Update(float dt)
    {
        for (int i = 0; i < _tracked.Count; i++)
        {
            var entity = _tracked[i];
            if (entity.IsNull) continue;
            if (!entity.TryGetComponent<CameraOrbitState>(out var orbit)) continue;
            InputIntent intent = default;
            entity.TryGetComponent<InputIntent>(out intent);

            orbit = Step(orbit, intent, dt);
            entity.GetComponent<CameraOrbitState>() = orbit;
        }
    }

    /// <summary>纯函数：累加输入增量、夹紧、平滑——可单测。</summary>
    public static CameraOrbitState Step(CameraOrbitState orbit, InputIntent intent, float dt)
    {
        if (intent.HasIntent != 0)
        {
            float pitchSign = orbit.InvertPitch != 0 ? 1f : -1f;
            orbit.TargetYaw -= intent.AimDeltaX * orbit.YawSensitivity;
            orbit.TargetPitch += intent.AimDeltaY * orbit.PitchSensitivity * pitchSign;
            orbit.TargetZoom -= intent.ZoomDelta * orbit.ZoomSensitivity;
        }

        // Clamp
        if (orbit.MaxPitch > orbit.MinPitch)
            orbit.TargetPitch = MathF.Min(MathF.Max(orbit.TargetPitch, orbit.MinPitch), orbit.MaxPitch);
        if (orbit.MaxZoom > orbit.MinZoom)
            orbit.TargetZoom = MathF.Min(MathF.Max(orbit.TargetZoom, orbit.MinZoom), orbit.MaxZoom);

        // Smooth: exponential approach. SmoothFactor in [0,1] per second.
        float k = orbit.SmoothFactor <= 0f ? 1f : 1f - MathF.Exp(-orbit.SmoothFactor * dt);
        orbit.Yaw += (orbit.TargetYaw - orbit.Yaw) * k;
        orbit.Pitch += (orbit.TargetPitch - orbit.Pitch) * k;
        orbit.Zoom += (orbit.TargetZoom - orbit.Zoom) * k;
        return orbit;
    }
}
