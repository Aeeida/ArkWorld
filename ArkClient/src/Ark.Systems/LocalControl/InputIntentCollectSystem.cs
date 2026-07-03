using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Ark.Ecs.Components;

namespace Ark.Systems.LocalControl;

/// <summary>
/// 输入意图采集 System (Phase 4)。
/// 在 _Process 阶段读取 Godot 输入设备并写入注册玩家实体的 <see cref="InputIntent"/> 组件。
/// 仅本系统直接接触 Godot.Input；下游 prediction/camera/combat 系统统一消费 InputIntent。
///
/// 设计为 opt-in：受控玩家通过 <see cref="Register"/> 注册其 ECS 实体；未注册时不写入。
/// </summary>
public sealed class InputIntentCollectSystem
{
    private readonly EntityStore _store;
    private readonly List<Source> _sources = new();

    /// <summary>
    /// 鼠标增量提供方接口（控制器在 _Input/_UnhandledInput 中累积，本 System 在 _Process 中消费并清零）。
    /// </summary>
    public interface IMouseDeltaSource
    {
        Entity LocalEntity { get; }
        /// <summary>本帧累计鼠标增量（消费后必须清零）。</summary>
        Vector2 ConsumeMouseDelta();
        /// <summary>本帧累计滚轮增量（消费后必须清零）。</summary>
        float ConsumeWheelDelta();
        /// <summary>是否抑制移动/动作输入（例如建造相机模式或鼠标未捕获）。</summary>
        bool SuppressGameplayInput();
    }

    private struct Source
    {
        public IMouseDeltaSource Provider;
    }

    public InputIntentCollectSystem(EntityStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public void Register(IMouseDeltaSource source)
    {
        if (source is null) return;
        for (int i = 0; i < _sources.Count; i++)
            if (ReferenceEquals(_sources[i].Provider, source))
                return;
        _sources.Add(new Source { Provider = source });
    }

    public void Unregister(IMouseDeltaSource source)
    {
        if (source is null) return;
        for (int i = 0; i < _sources.Count; i++)
        {
            if (ReferenceEquals(_sources[i].Provider, source))
            {
                _sources.RemoveAt(i);
                return;
            }
        }
    }

    public void Update()
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            var provider = _sources[i].Provider;
            var entity = provider.LocalEntity;
            if (entity.IsNull)
                continue;

            var mouseDelta = provider.ConsumeMouseDelta();
            float wheel = provider.ConsumeWheelDelta();
            bool suppressed = provider.SuppressGameplayInput();

            InputIntent intent = default;
            intent.AimDeltaX = mouseDelta.X;
            intent.AimDeltaY = mouseDelta.Y;
            intent.ZoomDelta = wheel;
            intent.HasIntent = (byte)(suppressed ? 0 : 1);

            if (!suppressed)
            {
                Vector2 move = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
                intent.MoveX = move.X;
                intent.MoveZ = move.Y;
                intent.SprintHeld = Pressed("sprint");
                intent.JumpJustPressed = JustPressed("jump");
                intent.InteractJustPressed = JustPressed("interact");
                intent.ReloadJustPressed = JustPressed("reload");
                intent.BuildToggleJustPressed = JustPressed("build_mode");
            }

            // 写回 ECS（不存在则添加）
            if (entity.HasComponent<InputIntent>())
            {
                ref var slot = ref entity.GetComponent<InputIntent>();
                slot = intent;
            }
            else
            {
                entity.AddComponent(intent);
            }
        }
    }

    private static byte Pressed(string action)
        => InputMap.HasAction(action) && Input.IsActionPressed(action) ? (byte)1 : (byte)0;

    private static byte JustPressed(string action)
        => InputMap.HasAction(action) && Input.IsActionJustPressed(action) ? (byte)1 : (byte)0;
}
