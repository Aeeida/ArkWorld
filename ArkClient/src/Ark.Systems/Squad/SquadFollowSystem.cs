using System;
using Godot;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;

namespace Ark.Systems.Squad;

/// <summary>
/// 小队跟随系统 — 计算队员的跟随目标位置并更新 AiMovement。
///
/// 执行逻辑：
/// 1. 获取队长位置和朝向
/// 2. 根据阵型偏移计算每个队员的目标位置
/// 3. 如果距离目标超过阈值，设置 AiMovement.IsMoving = 1
/// 4. 超远距离（>TeleportDistance）直接瞬移到编队位置（带地形检测）
/// </summary>
public sealed class SquadFollowSystem
{
    private readonly EntityStore _store;

    // ─── 配置 ───
    private float _moveThreshold  = 1.5f;   // 超过此距离才开始移动
    private float _stopThreshold  = 0.8f;   // 小于此距离停止
    private float _catchUpSpeed   = 10.0f;  // 追赶速度（远距离冲刺）
    private float _walkSpeed      = 5.5f;   // 正常跟随速度
    private const float SprintThreshold = 6f;    // 超过此距离直奔队长（不维持编队）
    private const float TeleportDistance = 25f;   // 超过此距离直接瞬移到队长身边
    private const float TeleportHeightMargin = 2f; // 瞬移后距地形的安全高度

    // ─── 队长引用（由外部设置）───
    private Entity  _leaderEntity;
    private Vector3 _leaderPosition;
    private float   _leaderYaw;

    // ─── 地形查询（可选，用于瞬移时安全高度）───
    private Func<float, float, float>? _sampleTerrainHeight;

    // ─── 延迟瞬移请求（避免在查询循环中做结构性更改）───
    private readonly List<(int entityId, float x, float y, float z)> _pendingTeleports = new();

    // ─── 载具瞬移请求 (vehicleEntityId → targetPos)，去重 ───
    private readonly Dictionary<int, (float x, float y, float z, float yaw)> _pendingVehicleTeleports = new();

    public SquadFollowSystem(EntityStore store)
    {
        _store = store;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          配置
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 设置跟随参数。
    /// </summary>
    public void Configure(float moveThreshold, float stopThreshold, float catchUpSpeed, float walkSpeed)
    {
        _moveThreshold = moveThreshold;
        _stopThreshold = stopThreshold;
        _catchUpSpeed  = catchUpSpeed;
        _walkSpeed     = walkSpeed;
    }

    /// <summary>
    /// 设置队长实体。
    /// </summary>
    public void SetLeader(Entity leaderEntity)
    {
        _leaderEntity = leaderEntity;
    }

    /// <summary>
    /// 设置地形高度查询函数（可选，用于瞬移时安全高度检测）。
    /// </summary>
    public void SetTerrainQuery(Func<float, float, float>? sampleHeight)
    {
        _sampleTerrainHeight = sampleHeight;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          每帧更新
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 更新所有跟随者的移动目标。
    /// 应在 GameBootstrap._Process 中调用。
    /// </summary>
    public void Update(float deltaTime)
    {
        // 获取队长位置和朝向
        if (_leaderEntity.Id == 0) return;

        // 如果队长在载具中，跟随载具位置（仅限真正的载具，排除火箭等）
        Entity followTarget = _leaderEntity;
        int leaderVehicleId = 0; // 领队所在载具 ID（0 = 不在载具中）
        if (_leaderEntity.TryGetComponent<VehicleSeat>(out var seat) && seat.VehicleEntityId > 0)
        {
            var vehicle = _store.GetEntityById(seat.VehicleEntityId);
            // 只有拥有 VehicleTag 的才是真正的载具，火箭/建筑等不算
            if (!vehicle.IsNull && vehicle.Tags.Has<VehicleTag>())
            {
                leaderVehicleId = seat.VehicleEntityId;
                followTarget = vehicle;
            }
        }

        if (!followTarget.TryGetComponent<WorldPosition>(out var leaderPos)) return;
        _leaderPosition = new Vector3(leaderPos.X, leaderPos.Y, leaderPos.Z);

        // 获取跟随目标朝向（用于计算阵型方向）
        if (followTarget.TryGetComponent<WorldRotation>(out var leaderRot))
        {
            var quat = new Quaternion(leaderRot.X, leaderRot.Y, leaderRot.Z, leaderRot.W);
            _leaderYaw = quat.GetEuler().Y;
        }
        else
        {
            _leaderYaw = 0f;
        }

        // 更新所有带 Following 标签的队员
        var query = _store.Query<WorldPosition, FormationOffset, AiMovement, SquadMember>()
            .AllTags(Tags.Get<Following>());

        // 收集需要瞬移的队员（不能在查询循环中做结构性更改）
        _pendingTeleports.Clear();
        _pendingVehicleTeleports.Clear();

        foreach (var chunk in query.Chunks)
        {
            var positions  = chunk.Chunk1;
            var offsets    = chunk.Chunk2;
            var movements  = chunk.Chunk3;
            var members    = chunk.Chunk4;

            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var pos      = ref positions.Span[i];
                ref readonly var offset   = ref offsets.Span[i];
                ref readonly var member   = ref members.Span[i];
                ref var          movement = ref movements.Span[i];

                // 跳过被玩家控制的队员
                if (member.IsControlled != 0) continue;

                // ── 检查载具状态（仅限真正的载具）──
                var followerEntity = _store.GetEntityById(chunk.Entities[i]);
                bool followerInVehicle = false;
                int  followerVehicleId = 0;
                if (!followerEntity.IsNull && followerEntity.TryGetComponent<VehicleSeat>(out var fSeat)
                    && fSeat.VehicleEntityId > 0)
                {
                    var fVehicle = _store.GetEntityById(fSeat.VehicleEntityId);
                    if (!fVehicle.IsNull && fVehicle.Tags.Has<VehicleTag>())
                    {
                        followerInVehicle = true;
                        followerVehicleId = fSeat.VehicleEntityId;
                    }
                }

                // ── 与领队在同一载具 → 不做跟随运动（已经在车上了）──
                if (followerInVehicle && leaderVehicleId > 0 && followerVehicleId == leaderVehicleId)
                {
                    movement = AiMovement.Idle(_leaderYaw);
                    continue;
                }

                // 计算到队长的直线距离
                // 若在载具中，用载具位置计算距离
                float curX = pos.X, curY = pos.Y, curZ = pos.Z;
                if (followerInVehicle && followerVehicleId > 0)
                {
                    var vEntity = _store.GetEntityById(followerVehicleId);
                    if (!vEntity.IsNull && vEntity.TryGetComponent<WorldPosition>(out var vp))
                    {
                        curX = vp.X; curY = vp.Y; curZ = vp.Z;
                    }
                }
                var currentPos = new Vector3(curX, curY, curZ);
                float distToLeader = (_leaderPosition - currentPos).Length();

                // ── 超远距离：记录瞬移请求（延迟到循环外执行）──
                if (distToLeader > TeleportDistance)
                {
                    var formPos = CalculateFormationPosition(offset);

                    // 地形安全高度检测
                    float safeY;
                    if (_sampleTerrainHeight != null)
                    {
                        float terrainY = _sampleTerrainHeight(formPos.X, formPos.Z);
                        safeY = MathF.Max(formPos.Y, terrainY + TeleportHeightMargin);
                    }
                    else
                    {
                        safeY = _leaderPosition.Y + TeleportHeightMargin;
                    }

                    // 标记为停止移动（这是 ref 写入，安全）
                    movement = AiMovement.Idle(_leaderYaw);

                    if (followerInVehicle && followerVehicleId > 0)
                    {
                        // 载具瞬移：整辆载具（含所有乘客）移到编队位置
                        // 用 Dictionary 去重：同一载具只瞬移一次
                        if (!_pendingVehicleTeleports.ContainsKey(followerVehicleId))
                            _pendingVehicleTeleports[followerVehicleId] = (formPos.X, safeY, formPos.Z, _leaderYaw);
                    }
                    else if (!followerEntity.IsNull)
                    {
                        // 步行队员瞬移
                        _pendingTeleports.Add((chunk.Entities[i], formPos.X, safeY, formPos.Z));
                    }

                    continue;
                }

                // ── 载具跟随使用更宽松的参数 ──
                float moveThreshold;
                float stopThreshold;
                float sprintThreshold;
                float speed;
                if (followerInVehicle)
                {
                    moveThreshold   = 6.0f;
                    stopThreshold   = 4.0f;
                    sprintThreshold = 20f;
                    speed = distToLeader > 20f ? _catchUpSpeed
                          : distToLeader > 10f ? _walkSpeed
                          :                      _walkSpeed * 0.5f;
                }
                else
                {
                    moveThreshold   = _moveThreshold;
                    stopThreshold   = _stopThreshold;
                    sprintThreshold = SprintThreshold;
                    speed = distToLeader > _moveThreshold * 2f ? _catchUpSpeed : _walkSpeed;
                }

                // 远距离：直奔队长；近距离：维持编队
                Vector3 targetPos = distToLeader > sprintThreshold
                    ? _leaderPosition
                    : CalculateFormationPosition(offset);

                var toTarget = targetPos - currentPos;
                toTarget.Y = 0;
                float dist = toTarget.Length();

                // 决定移动状态
                if (dist > moveThreshold)
                {
                    movement = AiMovement.MoveTo(targetPos.X, targetPos.Y, targetPos.Z, speed, _leaderYaw);
                }
                else if (dist < stopThreshold)
                {
                    movement = AiMovement.Idle(_leaderYaw);
                }
                else if (movement.IsMoving != 0)
                {
                    movement.UpdateTarget(targetPos.X, targetPos.Y, targetPos.Z, _leaderYaw);
                }
            }
        }

        // ── 在查询循环外执行延迟的瞬移操作（结构性更改安全）──

        // 步行队员瞬移
        foreach (var (entityId, tx, ty, tz) in _pendingTeleports)
        {
            var entity = _store.GetEntityById(entityId);
            if (entity.IsNull) continue;

            entity.AddComponent(new WorldPosition { X = tx, Y = ty, Z = tz });

            if (!entity.Tags.Has<Teleported>())
                entity.AddTag<Teleported>();
        }

        // 载具整体瞬移（含所有乘客）
        foreach (var (vehicleId, (vx, vy, vz, vyaw)) in _pendingVehicleTeleports)
        {
            var vehicleEntity = _store.GetEntityById(vehicleId);
            if (vehicleEntity.IsNull) continue;

            // 1. 直接更新载具 WorldPosition
            vehicleEntity.AddComponent(new WorldPosition { X = vx, Y = vy, Z = vz });

            // 2. 更新载具 WorldRotation 面朝领队方向
            var newQuat = new Quaternion(Vector3.Up, vyaw);
            vehicleEntity.AddComponent(new WorldRotation
            {
                X = newQuat.X, Y = newQuat.Y, Z = newQuat.Z, W = newQuat.W
            });

            // 3. 停止载具运动
            if (vehicleEntity.TryGetComponent<VehicleState>(out var vs))
            {
                vs.CurrentSpeed = 0;
                vehicleEntity.AddComponent(vs);
            }
            vehicleEntity.AddComponent(new Velocity { X = 0, Y = 0, Z = 0, Speed = 0 });

            // 4. 标记所有乘客为已瞬移（让 Godot 节点同步位置）
            //    乘客的 Godot 节点在 ProcessAiMovement/ProcessAiVehicleFollow 中
            //    会根据载具 WorldPosition + SeatOffset 自动同步位置
            if (!vehicleEntity.Tags.Has<Teleported>())
                vehicleEntity.AddTag<Teleported>();
        }
    }

    /// <summary>
    /// 计算阵型位置（世界坐标）。
    /// </summary>
    private Vector3 CalculateFormationPosition(in FormationOffset offset)
    {
        // 将本地阵型偏移旋转到队长朝向（Godot: forward = -Z）
        // local right  = (cos(yaw), 0, -sin(yaw))
        // local forward = (-sin(yaw), 0, -cos(yaw))
        float cos = Mathf.Cos(_leaderYaw);
        float sin = Mathf.Sin(_leaderYaw);

        float worldX =  offset.OffsetX * cos - offset.OffsetZ * sin;
        float worldZ = -offset.OffsetX * sin - offset.OffsetZ * cos;

        return new Vector3(
            _leaderPosition.X + worldX,
            _leaderPosition.Y,
            _leaderPosition.Z + worldZ
        );
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          命令
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 让所有队员停止跟随（原地待命）。
    /// </summary>
    public void HoldPosition()
    {
        var query = _store.Query<AiMovement>().AllTags(Tags.Get<SquadFollower>());

        foreach (var (movements, entities) in query.Chunks)
        {
            for (int i = 0; i < entities.Length; i++)
            {
                movements.Span[i] = AiMovement.Idle(movements.Span[i].FacingYaw);

                var entity = _store.GetEntityById(entities[i]);
                entity.RemoveTag<Following>();
                entity.AddTag<HoldPosition>();
            }
        }
    }

    /// <summary>
    /// 让所有队员恢复跟随。
    /// </summary>
    public void ResumeFollow()
    {
        var query = _store.Query().AllTags(Tags.Get<HoldPosition>());

        foreach (var entity in query.Entities)
        {
            entity.RemoveTag<HoldPosition>();
            entity.AddTag<Following>();
        }
    }

    /// <summary>
    /// 让所有队员移动到指定位置。
    /// </summary>
    public void MoveAllTo(Vector3 target, float speed = 5f)
    {
        var query = _store.Query<AiMovement>().AllTags(Tags.Get<SquadFollower>());

        foreach (var (movements, entities) in query.Chunks)
        {
            for (int i = 0; i < entities.Length; i++)
            {
                movements.Span[i] = AiMovement.MoveTo(target.X, target.Y, target.Z, speed, 0f);

                var entity = _store.GetEntityById(entities[i]);
                entity.RemoveTag<Following>();
                entity.RemoveTag<HoldPosition>();
            }
        }
    }

    /// <summary>
    /// 命令所有队员锁定指定目标实体（领队下达攻击命令）。
    /// </summary>
    public void CommandAttackTarget(int targetEntityId)
    {
        if (targetEntityId <= 0) return;

        var targetEntity = _store.GetEntityById(targetEntityId);
        if (targetEntity.IsNull) return;

        // 获取目标位置作为瞄准点
        float aimX = 0, aimY = 0, aimZ = 0;
        if (targetEntity.TryGetComponent<WorldPosition>(out var tPos))
        {
            aimX = tPos.X;
            aimY = tPos.Y + 1.2f; // 瞄准体中心
            aimZ = tPos.Z;
        }

        var query = _store.Query<CombatTarget, SquadMember>()
            .AllTags(Tags.Get<SquadFollower>());

        foreach (var chunk in query.Chunks)
        {
            var targets = chunk.Chunk1;
            var members = chunk.Chunk2;

            for (int i = 0; i < chunk.Length; i++)
            {
                if (members.Span[i].IsControlled != 0) continue;

                targets.Span[i] = new CombatTarget
                {
                    TargetEntityId = targetEntityId,
                    AimPointX = aimX,
                    AimPointY = aimY,
                    AimPointZ = aimZ,
                    HasTarget = 1,
                    IsCommandTarget = 1,
                };
            }
        }
    }

    /// <summary>
    /// 清除队员的命令目标（恢复为跟随领队目标）。
    /// </summary>
    public void ClearCommandTarget()
    {
        var query = _store.Query<CombatTarget, SquadMember>()
            .AllTags(Tags.Get<SquadFollower>());

        foreach (var chunk in query.Chunks)
        {
            var targets = chunk.Chunk1;
            var members = chunk.Chunk2;

            for (int i = 0; i < chunk.Length; i++)
            {
                if (members.Span[i].IsControlled != 0) continue;

                // 仅清除命令指定的目标
                if (targets.Span[i].IsCommandTarget != 0)
                    targets.Span[i] = CombatTarget.None;
            }
        }
    }

    /// <summary>
    /// 同步队员的瞄准目标 — 无命令目标时跟随领队的目标。
    /// 应在 Update() 之后调用。
    /// </summary>
    public void SyncMemberTargets()
    {
        // 读取领队的目标
        if (_leaderEntity.Id == 0) return;
        if (!_leaderEntity.TryGetComponent<CombatTarget>(out var leaderTarget)) return;

        var query = _store.Query<CombatTarget, SquadMember>()
            .AllTags(Tags.Get<SquadFollower>());

        foreach (var chunk in query.Chunks)
        {
            var targets = chunk.Chunk1;
            var members = chunk.Chunk2;

            for (int i = 0; i < chunk.Length; i++)
            {
                if (members.Span[i].IsControlled != 0) continue;

                ref var target = ref targets.Span[i];

                // 有命令目标时不覆盖
                if (target.IsCommandTarget != 0)
                {
                    // 但需更新命令目标的最新位置
                    if (target.TargetEntityId > 0)
                    {
                        var cmdTarget = _store.GetEntityById(target.TargetEntityId);
                        if (!cmdTarget.IsNull && cmdTarget.TryGetComponent<WorldPosition>(out var cmdPos))
                        {
                            target.AimPointX = cmdPos.X;
                            target.AimPointY = cmdPos.Y + 1.2f;
                            target.AimPointZ = cmdPos.Z;
                        }
                    }
                    continue;
                }

                // 跟随领队目标
                if (leaderTarget.HasTarget != 0)
                {
                    target.TargetEntityId = leaderTarget.TargetEntityId;
                    target.AimPointX = leaderTarget.AimPointX;
                    target.AimPointY = leaderTarget.AimPointY;
                    target.AimPointZ = leaderTarget.AimPointZ;
                    target.HasTarget = 1;
                }
                else
                {
                    target.HasTarget = 0;
                    target.TargetEntityId = -1;
                }
            }
        }
    }
}
