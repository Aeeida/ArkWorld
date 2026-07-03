using Godot;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;

namespace Ark.Player;

public partial class TpsPlayerController
{
    // ═══════════════════════════════════════════════════════════════════════
    //                          射击 / 瞄准 / 战斗目标
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 处理射击逻辑（持续开火时每帧尝试射击）。
    /// </summary>
    private void ProcessShooting(float dt)
    {
        if (_combatModule == null || _store == null || _entity.Id == 0) return;
        if (!_isFiring || _buildCameraMode || IsVehicleControlActive()) return;

        // 射击方向：从武器口到屏幕中心射线命中点（瞄准心）
        var aimOrigin = GlobalPosition + new Vector3(0, 1.4f, 0);
        var aimDir = GetAimDirectionTowardCrosshair(aimOrigin);
        var sysOrigin = new System.Numerics.Vector3(aimOrigin.X, aimOrigin.Y, aimOrigin.Z);
        var sysDir = new System.Numerics.Vector3(aimDir.X, aimDir.Y, aimDir.Z);

        // 网络模式：通过 ServerAuthorityBridge 路由到服务端
        if (Ark.Services.GameServices.IsNetworkMode)
        {
            var weaponDefId = ResolveCurrentWeaponDefId();
            WriteNetworkWeaponFireCommand(weaponDefId, aimOrigin, aimDir);
            return;
        }

        _combatModule.TryFire(_entity.Id, sysOrigin, sysDir, _combatModule.GameTime);
    }

    /// <summary>
    /// 获取准心在屏幕上的实际位置（考虑缩放偏移）。
    /// </summary>
    private Vector2 GetCrosshairScreenPosition()
    {
        if (_crosshairWidget != null)
            return _crosshairWidget.GetGlobalRect().GetCenter();
        // 回退：屏幕正中央
        return GetViewport().GetVisibleRect().Size * 0.5f;
    }

    /// <summary>
    /// 更新十字准心颜色 — 瞄准敌人（Hostile 标签且碰撞层 4）时变红。
    /// 同时将准心射线结果同步到 ECS CombatTarget 组件（领队/载具共用）。
    /// </summary>
    private void SyncCombatTarget()
    {
        if (_store == null || _entity.Id == 0) return;

        var cam = _useGlobalCamera ? _globalCamera?.Camera : _camera;
        if (cam == null) return;

        var spaceState = cam.GetWorld3D()?.DirectSpaceState;
        if (spaceState == null) return;

        var crosshairPos = GetCrosshairScreenPosition();
        var rayOrigin = cam.ProjectRayOrigin(crosshairPos);
        var rayDir = cam.ProjectRayNormal(crosshairPos);

        // 全碰撞层射线 — 找到准心指向的世界碰撞点
        var rayEnd = rayOrigin + rayDir * 500f;
        var queryAll = PhysicsRayQueryParameters3D.Create(rayOrigin, rayEnd);
        queryAll.CollisionMask = 0xFFFFFFFF;
        queryAll.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        queryAll.HitFromInside = false;
        var resultAll = spaceState.IntersectRay(queryAll);

        var target = CombatTarget.None;

        if (resultAll.Count > 0)
        {
            var hitPoint = (Vector3)resultAll["position"];
            target.AimPointX = hitPoint.X;
            target.AimPointY = hitPoint.Y;
            target.AimPointZ = hitPoint.Z;
            target.HasTarget = 1;

            // 检查命中的是否为实体碰撞体
            if (resultAll.TryGetValue("collider", out var colliderVar) &&
                colliderVar.Obj is CollisionObject3D collider)
            {
                // 检查敌人碰撞层 (Layer 4 = bit 2)
                if ((collider.CollisionLayer & 4) != 0)
                {
                    _crosshairWidget?.SetEnemyHover(true);
                }
                else
                {
                    _crosshairWidget?.SetEnemyHover(false);
                }
            }
            else
            {
                _crosshairWidget?.SetEnemyHover(false);
            }
        }
        else
        {
            // 未命中：瞄准远方
            var farPoint = rayOrigin + rayDir * 200f;
            target.AimPointX = farPoint.X;
            target.AimPointY = farPoint.Y;
            target.AimPointZ = farPoint.Z;
            target.HasTarget = 0;
            _crosshairWidget?.SetEnemyHover(false);
        }

        // 写入 ECS
        int targetEntityId = IsVehicleControlActive() ? ResolveEffectiveVehicleEntityId() : _entity.Id;
        var targetEntity = _store.GetEntityById(targetEntityId);
        if (!targetEntity.IsNull)
            _ecsAuth?.Write(targetEntity, target);
    }

    /// <summary>
    /// 获取相机朝向的世界方向（用于 ECS 同步）。
    /// </summary>
    public Vector3 GetAimDirection()
    {
        if (_camera == null) return -Transform.Basis.Z;
        return -_camera.GlobalTransform.Basis.Z;
    }

    /// <summary>
    /// 获取从武器出发点指向准心命中点的方向。
    /// 射线从准心实际位置投射到场景中，找到命中点后计算武器→命中点的方向。
    /// 如果射线未命中（天空等），则使用远距离点。
    /// </summary>
    public Vector3 GetAimDirectionTowardCrosshair(Vector3 weaponOrigin)
    {
        var cam = Camera; // 使用正确的相机（可能是全局相机）
        if (cam == null) return -Transform.Basis.Z;

        var crosshairPos = GetCrosshairScreenPosition();

        var rayOrigin = cam.ProjectRayOrigin(crosshairPos);
        var rayDir = cam.ProjectRayNormal(crosshairPos);

        // 尝试物理射线检测（排除自身碰撞体）
        var spaceState = cam.GetWorld3D()?.DirectSpaceState;
        if (spaceState != null)
        {
            var rayEnd = rayOrigin + rayDir * 500f;
            var query = PhysicsRayQueryParameters3D.Create(rayOrigin, rayEnd);
            query.CollisionMask = 0xFFFFFFFF;
            // 排除射击者自身（避免命中自己的碰撞体）
            query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
            query.HitFromInside = false;
            var result = spaceState.IntersectRay(query);
            if (result.Count > 0)
            {
                var hitPoint = (Vector3)result["position"];
                var dir = (hitPoint - weaponOrigin);
                // 如果命中点在武器后方（极近距离），忽略
                if (dir.LengthSquared() > 1f)
                    return dir.Normalized();
            }
        }

        // 未命中：指向远处（相机前方 200m）
        var farPoint = rayOrigin + rayDir * 200f;
        return (farPoint - weaponOrigin).Normalized();
    }

    /// <summary>
    /// 获取移动输入方向（用于 ECS 同步）。
    /// </summary>
    public (Vector3 direction, float speed) GetMoveInput()
    {
        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
        if (inputDir == Vector2.Zero)
            return (Vector3.Zero, 0);

        Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
        float speed = Input.IsActionPressed("sprint") ? SprintSpeed : WalkSpeed;
        return (direction, speed);
    }
}
