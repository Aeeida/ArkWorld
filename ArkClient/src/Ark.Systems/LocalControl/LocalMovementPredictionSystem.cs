using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Player.Character;

namespace Ark.Systems.LocalControl;

/// <summary>
/// 本地玩家运动预测 System (Phase 4)。
/// 读取 <see cref="InputIntent"/> + <see cref="LocalMovementPredict"/>，
/// 应用 <see cref="CharacterMotion"/> 计算下一帧速度，写回 <see cref="LocalMovementPredict"/>。
///
/// 不直接做碰撞步进——下游控制器读取最新 <see cref="LocalMovementPredict.VelocityX/Y/Z"/> 并调用 MoveAndSlide，
/// 然后把碰撞反馈后的速度写回组件以供下一帧累积。
/// 控制器在 _PhysicsProcess 调用 <see cref="StepEntity(Entity, float)"/>；本类亦提供 batch <see cref="Update(float)"/>。
/// </summary>
public sealed class LocalMovementPredictionSystem
{
    private readonly EntityStore _store;
    private readonly List<Entity> _tracked = new();

    public LocalMovementPredictionSystem(EntityStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>注册本地玩家实体（控制器在 _Ready 中调用）。</summary>
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

    /// <summary>批量推进所有 tracked 实体的预测一步。</summary>
    public void Update(float dt)
    {
        for (int i = 0; i < _tracked.Count; i++)
            StepEntity(_tracked[i], dt);
    }

    /// <summary>对单个实体推进一步预测；缺组件时静默跳过。</summary>
    public static void StepEntity(Entity entity, float dt)
    {
        if (entity.IsNull) return;
        if (!entity.TryGetComponent<LocalMovementPredict>(out var predict))
            return;
        if (!entity.TryGetComponent<InputIntent>(out var intent))
            intent = default;

        predict = Step(predict, intent, dt);
        entity.GetComponent<LocalMovementPredict>() = predict;
    }

    /// <summary>纯函数预测步骤——无 Godot/无 EntityStore 依赖，便于单元测试。</summary>
    public static LocalMovementPredict Step(LocalMovementPredict predict, InputIntent intent, float dt)
    {
        bool hasInput = intent.HasIntent != 0 && (intent.MoveX != 0f || intent.MoveZ != 0f);
        bool isSprinting = intent.HasIntent != 0 && intent.SprintHeld != 0;

        float targetSpeed = CharacterMotion.TargetSpeed(isSprinting, predict.WalkSpeed, predict.SprintSpeed);

        // 输入向量已经是角色局部空间（X strafe, Z forward）。控制器负责把它旋转到世界空间后再调 MoveAndSlide。
        // 但对于"目标速度"这一步，我们只需要量级方向；预测系统输出 desired velocity，
        // 由控制器结合相机/角色基矩阵转换到世界空间。
        float dirLen = MathF.Sqrt(intent.MoveX * intent.MoveX + intent.MoveZ * intent.MoveZ);
        float invLen = dirLen > 0f ? 1f / dirLen : 0f;
        float desiredVx = intent.MoveX * invLen * targetSpeed;
        float desiredVz = intent.MoveZ * invLen * targetSpeed;

        bool isOnFloor = predict.IsOnFloor != 0;

        var (newVx, newVz) = CharacterMotion.ApplyHorizontal(
            predict.VelocityX, predict.VelocityZ,
            desiredVx, desiredVz,
            predict.Acceleration, predict.Deceleration, predict.AirControl,
            isOnFloor, hasInput, dt);

        var (newVy, jumpsAfterGravity) = CharacterMotion.ApplyGravity(
            predict.VelocityY, predict.Gravity, dt,
            isOnFloor, predict.JumpsRemaining, predict.MaxJumps);

        bool jumpReq = intent.HasIntent != 0 && intent.JumpJustPressed != 0;
        var (finalVy, jumpsAfterJump) = CharacterMotion.TryJump(
            newVy, predict.JumpVelocity, jumpsAfterGravity, jumpReq);

        predict.VelocityX = newVx;
        predict.VelocityY = finalVy;
        predict.VelocityZ = newVz;
        predict.DesiredVelocityX = desiredVx;
        predict.DesiredVelocityZ = desiredVz;
        predict.JumpsRemaining = jumpsAfterJump;
        predict.JumpRequested = jumpReq ? (byte)1 : (byte)0;
        return predict;
    }
}
