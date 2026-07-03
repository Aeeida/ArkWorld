using System;
using System.Collections.Generic;
using System.Numerics;
using Friflo.Engine.ECS;
using Ark.Abstractions;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Shared.Data;

namespace Ark.Gameplay.Vehicle;

/// <summary>
/// 武器装备委托 — 用于解耦 VehicleSystem 与 WeaponSystem。
/// 由 GameBootstrap / CombatGameplayModule 注入。
/// </summary>
public delegate void EquipWeaponDelegate(int entityId, int weaponDefId, int slotIndex = 0);

/// <summary>
/// 载具系统 — 管理载具的进入/退出、驾驶、炮塔控制。
/// </summary>
public sealed class VehicleSystem
{
    private readonly EntityStore _store;
    private readonly VehicleDefRegistry _vehicleDefs;
    private readonly EquipWeaponDelegate? _equipWeapon;
    private ITerrainQuery? _terrain;

    public event Action<VehicleEnterExitEvent>? OnVehicleEntered;
    public event Action<VehicleEnterExitEvent>? OnVehicleExited;
    public event Action<int>? OnVehicleDestroyed;

    /// <summary>设置地形查询（延迟注入）。</summary>
    public void SetTerrainQuery(ITerrainQuery? terrain) => _terrain = terrain;

    public VehicleSystem(EntityStore store, VehicleDefRegistry vehicleDefs, EquipWeaponDelegate? equipWeapon = null)
    {
        _store = store;
        _vehicleDefs = vehicleDefs;
        _equipWeapon = equipWeapon;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          每帧更新
    // ═══════════════════════════════════════════════════════════════════════

    public void Update(float deltaTime)
    {
        UpdateVehiclePhysics(deltaTime);
        UpdateTurrets(deltaTime);
        UpdateFuel(deltaTime);
    }

    private void UpdateVehiclePhysics(float deltaTime)
    {
        var query = _store.Query<VehicleState, WorldPosition, Velocity, WorldRotation>()
            .AllTags(Tags.Get<VehicleTag>());

        foreach (var chunk in query.Chunks)
        {
            var vehicles   = chunk.Chunk1;
            var velocities = chunk.Chunk3;

            for (int i = 0; i < chunk.Length; i++)
            {
                ref var vehicle = ref vehicles.Span[i];
                ref var vel     = ref velocities.Span[i];

                if (vehicle.IsOperational == 0) continue;
                if (vehicle.FuelCurrent <= 0) continue;

                float speed = MathF.Sqrt(vel.X * vel.X + vel.Z * vel.Z);
                vehicle.CurrentSpeed = speed;

                if (speed > vehicle.MaxSpeed)
                {
                    float scale = vehicle.MaxSpeed / speed;
                    vel.X *= scale;
                    vel.Z *= scale;
                }
            }
        }
    }

    private void UpdateTurrets(float deltaTime)
    {
        var query = _store.Query<TurretState>();

        foreach (var chunk in query.Chunks)
        {
            var turrets = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref var turret = ref turrets.Span[i];
                turret.Pitch = Math.Clamp(turret.Pitch, turret.MinPitch, turret.MaxPitch);
            }
        }
    }

    private void UpdateFuel(float deltaTime)
    {
        var query = _store.Query<VehicleState>().AllTags(Tags.Get<VehicleTag>());

        foreach (var chunk in query.Chunks)
        {
            var vehicles = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref var vehicle = ref vehicles.Span[i];
                if (vehicle.CurrentSpeed > 0.1f && vehicle.FuelCurrent > 0)
                {
                    var def = _vehicleDefs.Get(vehicle.VehicleDefId);
                    float consumption = def?.FuelConsumption ?? 0.1f;
                    vehicle.FuelCurrent -= consumption * deltaTime;
                    if (vehicle.FuelCurrent < 0) vehicle.FuelCurrent = 0;
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          进入/退出载具
    // ═══════════════════════════════════════════════════════════════════════

    public bool TryEnterVehicle(int characterEntityId, int vehicleEntityId, int preferredSeat = 0)
    {
        var character = _store.GetEntityById(characterEntityId);
        var vehicle   = _store.GetEntityById(vehicleEntityId);
        if (character.IsNull || vehicle.IsNull) return false;

        if (!vehicle.TryGetComponent<VehicleState>(out var vehicleState)) return false;
        if (vehicleState.IsOperational == 0) return false;

        var def = _vehicleDefs.Get(vehicleState.VehicleDefId);
        if (def == null) return false;

        int seatIndex = FindAvailableSeat(vehicleEntityId, preferredSeat, def.Value.Seats);
        if (seatIndex < 0) return false;

        var seatDef = def.Value.Seats[seatIndex];

        character.AddComponent(new VehicleSeat
        {
            VehicleEntityId = vehicleEntityId,
            SeatIndex       = (byte)seatIndex,
            SeatType        = (byte)seatDef.Type,
            OffsetX         = seatDef.LocalOffset.X,
            OffsetY         = seatDef.LocalOffset.Y,
            OffsetZ         = seatDef.LocalOffset.Z,
        });
        character.AddTag<InVehicle>();

        switch (seatDef.Type)
        {
            case SeatType.Driver:    character.AddTag<IsDriver>(); break;
            case SeatType.Gunner:    character.AddTag<IsGunner>(); break;
            case SeatType.Passenger: character.AddTag<IsPassenger>(); break;
        }

        if (seatDef.HasWeapon && seatDef.WeaponDefId > 0)
        {
            // 保存角色原有武器 / 弹药，退出载具时恢复
            if (character.TryGetComponent<WeaponState>(out var prevWs) &&
                character.TryGetComponent<AmmoState>(out var prevAmmo))
            {
                character.AddComponent(new PersonalWeapon
                {
                    WeaponDefId = prevWs.WeaponDefId,
                    Category    = prevWs.Category,
                    SlotIndex   = prevWs.SlotIndex,
                    CurrentMag  = prevAmmo.CurrentMag,
                    MagCapacity = prevAmmo.MagCapacity,
                    ReserveAmmo = prevAmmo.ReserveAmmo,
                    MaxReserve  = prevAmmo.MaxReserve,
                });
            }
            // 移除角色武器（载具武器在载具实体上，不放在角色上）
            character.RemoveComponent<WeaponState>();
            character.RemoveComponent<AmmoState>();
        }

        OnVehicleEntered?.Invoke(new VehicleEnterExitEvent(
            characterEntityId, vehicleEntityId, seatIndex, true));
        return true;
    }

    public bool TryExitVehicle(int characterEntityId)
    {
        var character = _store.GetEntityById(characterEntityId);
        if (character.IsNull) return false;
        if (!character.TryGetComponent<VehicleSeat>(out var seat)) return false;

        int vehicleId = seat.VehicleEntityId;
        int seatIndex = seat.SeatIndex;

        character.RemoveComponent<VehicleSeat>();
        character.RemoveTag<InVehicle>();
        character.RemoveTag<IsDriver>();
        character.RemoveTag<IsGunner>();
        character.RemoveTag<IsPassenger>();

        // 恢复角色原有武器 / 弹药
        if (character.TryGetComponent<PersonalWeapon>(out var pw))
        {
            _equipWeapon?.Invoke(characterEntityId, pw.WeaponDefId, pw.SlotIndex);
            // 恢复实际弹药数（EquipWeapon 会给满弹药，覆盖为存档值）
            character.AddComponent(new AmmoState
            {
                CurrentMag  = pw.CurrentMag,
                MagCapacity = pw.MagCapacity,
                ReserveAmmo = pw.ReserveAmmo,
                MaxReserve  = pw.MaxReserve,
            });
            character.RemoveComponent<PersonalWeapon>();
        }

        OnVehicleExited?.Invoke(new VehicleEnterExitEvent(
            characterEntityId, vehicleId, seatIndex, false));
        return true;
    }

    public bool TrySwitchSeat(int characterEntityId, int targetSeatIndex)
    {
        var character = _store.GetEntityById(characterEntityId);
        if (character.IsNull) return false;
        if (!character.TryGetComponent<VehicleSeat>(out var currentSeat)) return false;

        int vehicleId = currentSeat.VehicleEntityId;
        var vehicleDef = GetVehicleDefForEntity(vehicleId);
        if (vehicleDef == null) return false;

        if (targetSeatIndex >= vehicleDef.Value.Seats.Length) return false;
        if (!IsSeatAvailable(vehicleId, targetSeatIndex)) return false;

        var seatDef = vehicleDef.Value.Seats[targetSeatIndex];

        character.RemoveTag<IsDriver>();
        character.RemoveTag<IsGunner>();
        character.RemoveTag<IsPassenger>();

        character.AddComponent(new VehicleSeat
        {
            VehicleEntityId = vehicleId,
            SeatIndex       = (byte)targetSeatIndex,
            SeatType        = (byte)seatDef.Type,
            OffsetX         = seatDef.LocalOffset.X,
            OffsetY         = seatDef.LocalOffset.Y,
            OffsetZ         = seatDef.LocalOffset.Z,
        });

        switch (seatDef.Type)
        {
            case SeatType.Driver:    character.AddTag<IsDriver>(); break;
            case SeatType.Gunner:    character.AddTag<IsGunner>(); break;
            case SeatType.Passenger: character.AddTag<IsPassenger>(); break;
        }
        return true;
    }

    /// <summary>
    /// 循环切换到下一个可用座位（TAB 键）。
    /// </summary>
    public bool TryCycleToNextSeat(int characterEntityId)
    {
        var character = _store.GetEntityById(characterEntityId);
        if (character.IsNull) return false;
        if (!character.TryGetComponent<VehicleSeat>(out var currentSeat)) return false;

        var vehicleDef = GetVehicleDefForEntity(currentSeat.VehicleEntityId);
        if (vehicleDef == null) return false;

        int seatCount = vehicleDef.Value.Seats.Length;
        int current = currentSeat.SeatIndex;

        // 从当前座位 +1 开始遍历，寻找下一个可用座位
        for (int offset = 1; offset < seatCount; offset++)
        {
            int next = (current + offset) % seatCount;
            if (IsSeatAvailable(currentSeat.VehicleEntityId, next))
                return TrySwitchSeat(characterEntityId, next);
        }
        return false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          驾驶输入
    // ═══════════════════════════════════════════════════════════════════════

    public void ProcessDriveInput(int vehicleEntityId, float throttle, float steering, float brake)
    {
        var vehicle = _store.GetEntityById(vehicleEntityId);
        if (vehicle.IsNull) return;
        if (!vehicle.TryGetComponent<VehicleState>(out var state)) return;
        if (!vehicle.TryGetComponent<Velocity>(out var vel)) return;
        if (!vehicle.TryGetComponent<WorldRotation>(out var rot)) return;

        if (state.FuelCurrent <= 0) return;

        var def = _vehicleDefs.Get(state.VehicleDefId);
        float accel     = def?.Acceleration ?? 5f;
        float turnSpeed = def?.TurnSpeed ?? 2f;

        var quat = new Quaternion(rot.X, rot.Y, rot.Z, rot.W);
        var euler = QuaternionToEuler(quat);
        euler.Y += steering * turnSpeed * 0.016f;
        quat = EulerToQuaternion(euler);
        rot = new WorldRotation { X = quat.X, Y = quat.Y, Z = quat.Z, W = quat.W };

        var forward = Vector3.Transform(-Vector3.UnitZ, quat);
        forward.Y = 0;
        forward = Vector3.Normalize(forward);

        float targetSpeed  = throttle * state.MaxSpeed;
        float currentSpeed = state.CurrentSpeed;

        if (brake > 0)
            currentSpeed = Math.Max(currentSpeed - brake * accel * 2f * 0.016f, 0);
        else
            currentSpeed += (targetSpeed - currentSpeed) * accel * 0.016f;

        vel.X = forward.X * currentSpeed;
        vel.Z = forward.Z * currentSpeed;
        vel.Speed = currentSpeed;

        vehicle.AddComponent(vel);
        vehicle.AddComponent(rot);
    }

    public void ProcessTurretInput(int vehicleEntityId, float yawDelta, float pitchDelta)
    {
        var vehicle = _store.GetEntityById(vehicleEntityId);
        if (vehicle.IsNull) return;
        if (!vehicle.TryGetComponent<TurretState>(out var turret)) return;

        turret.Yaw   += yawDelta * turret.YawSpeed * 0.016f;
        turret.Pitch += pitchDelta * turret.PitchSpeed * 0.016f;
        turret.Pitch  = Math.Clamp(turret.Pitch, turret.MinPitch, turret.MaxPitch);

        vehicle.AddComponent(turret);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          生成载具
    // ═══════════════════════════════════════════════════════════════════════

    public Entity SpawnVehicle(int vehicleDefId, Vector3 position, Quaternion rotation)
    {
        var def = _vehicleDefs.Get(vehicleDefId);
        if (def == null) return default;

        // ── 校正 Y 到地形表面 ──
        float spawnY = position.Y;
        if (_terrain != null && VehicleTerrainSystem.IsGroundVehicle(def.Value.Type))
            spawnY = _terrain.SampleHeight(position.X, position.Z);

        // ── 检查与已有载具的间距（最小 8m）──
        var finalPos = FindSafeSpawnPosition(position.X, position.Z, spawnY, minDistance: 8f);

        var entity = _store.CreateEntity();

        entity.AddComponent(new VehicleState
        {
            VehicleDefId  = vehicleDefId,
            VehicleType   = (byte)def.Value.Type,
            MaxSpeed      = def.Value.MaxSpeed,
            HealthCurrent = def.Value.MaxHealth,
            HealthMax     = def.Value.MaxHealth,
            FuelCurrent   = def.Value.MaxFuel,
            FuelMax       = def.Value.MaxFuel,
            IsOperational = 1,
        });

        entity.AddComponent(new WorldPosition { X = finalPos.X, Y = finalPos.Y, Z = finalPos.Z });
        entity.AddComponent(new WorldRotation { X = rotation.X, Y = rotation.Y, Z = rotation.Z, W = rotation.W });
        entity.AddComponent(new Velocity());

        entity.AddComponent(new Health
        {
            Current = def.Value.MaxHealth,
            Max     = def.Value.MaxHealth,
        });

        entity.AddComponent(new Armor
        {
            Current           = 100f,
            Max               = 100f,
            PhysicalReduction = def.Value.ArmorPhysical,
            EnergyReduction   = def.Value.ArmorEnergy,
        });

        entity.AddTag<VehicleTag>();

        // 碰撞包围盒（供投射物碰撞检测使用）
        var bbox = def.Value.Type switch
        {
            VehicleType.Tank    => new BoundingBox { MinX = -2f, MinY = 0f, MinZ = -3f, MaxX = 2f, MaxY = 2.5f, MaxZ = 3f },
            VehicleType.Plane   => new BoundingBox { MinX = -4f, MinY = 0f, MinZ = -3f, MaxX = 4f, MaxY = 2f,   MaxZ = 3f },
            VehicleType.Boat    => new BoundingBox { MinX = -2f, MinY = 0f, MinZ = -4f, MaxX = 2f, MaxY = 2f,   MaxZ = 4f },
            VehicleType.Rocket  => new BoundingBox { MinX = -1.5f, MinY = 0f, MinZ = -1.5f, MaxX = 1.5f, MaxY = 6f, MaxZ = 1.5f },
            VehicleType.AntiAir => new BoundingBox { MinX = -1.5f, MinY = 0f, MinZ = -1.5f, MaxX = 1.5f, MaxY = 3f, MaxZ = 1.5f },
            _                   => new BoundingBox { MinX = -1.5f, MinY = 0f, MinZ = -2f, MaxX = 1.5f, MaxY = 2f, MaxZ = 2f },
        };
        entity.AddComponent(bbox);

        if (def.Value.TurretWeaponDefIds.Length > 0)
        {
            entity.AddComponent(new TurretState
            {
                YawSpeed    = 2f,
                PitchSpeed  = 1.5f,
                MinPitch    = -0.2f,
                MaxPitch    = 0.8f,
                WeaponDefId = def.Value.TurretWeaponDefIds[0],
            });

            // 载具自身持有武器 / 弹药（与角色武器独立）
            _equipWeapon?.Invoke(entity.Id, def.Value.TurretWeaponDefIds[0]);
        }

        return entity;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          辅助方法
    // ═══════════════════════════════════════════════════════════════════════

    private int FindAvailableSeat(int vehicleEntityId, int preferred, VehicleSeatDef[] seats)
    {
        if (preferred < seats.Length && IsSeatAvailable(vehicleEntityId, preferred))
            return preferred;
        for (int i = 0; i < seats.Length; i++)
            if (IsSeatAvailable(vehicleEntityId, i)) return i;
        return -1;
    }

    private bool IsSeatAvailable(int vehicleEntityId, int seatIndex)
    {
        var query = _store.Query<VehicleSeat>().AllTags(Tags.Get<InVehicle>());
        foreach (var chunk in query.Chunks)
        {
            var seats = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var seat = ref seats.Span[i];
                if (seat.VehicleEntityId == vehicleEntityId && seat.SeatIndex == seatIndex)
                    return false;
            }
        }
        return true;
    }

    private VehicleDef? GetVehicleDefForEntity(int vehicleEntityId)
    {
        var vehicle = _store.GetEntityById(vehicleEntityId);
        if (vehicle.IsNull) return null;
        if (!vehicle.TryGetComponent<VehicleState>(out var state)) return null;
        return _vehicleDefs.Get(state.VehicleDefId);
    }

    private static Vector3 QuaternionToEuler(Quaternion q)
    {
        float sinr = 2 * (q.W * q.X + q.Y * q.Z);
        float cosr = 1 - 2 * (q.X * q.X + q.Y * q.Y);
        float roll = MathF.Atan2(sinr, cosr);
        float sinp = 2 * (q.W * q.Y - q.Z * q.X);
        float pitch = MathF.Abs(sinp) >= 1 ? MathF.CopySign(MathF.PI / 2, sinp) : MathF.Asin(sinp);
        float siny = 2 * (q.W * q.Z + q.X * q.Y);
        float cosy = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
        float yaw = MathF.Atan2(siny, cosy);
        return new Vector3(roll, yaw, pitch);
    }

    private static Quaternion EulerToQuaternion(Vector3 euler)
    {
        return Quaternion.CreateFromYawPitchRoll(euler.Y, euler.Z, euler.X);
    }

    /// <summary>
    /// 寻找安全的载具生成位置 — 避开已有载具。
    /// 从请求位置开始，向外螺旋搜索直到找到无碰撞的位置。
    /// </summary>
    private Vector3 FindSafeSpawnPosition(float x, float z, float y, float minDistance)
    {
        // 收集所有现有载具位置
        var existingPositions = new List<Vector3>();
        var existQuery = _store.Query<WorldPosition>().AllTags(Tags.Get<VehicleTag>());
        foreach (var chunk in existQuery.Chunks)
        {
            var positions = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var p = ref positions.Span[i];
                existingPositions.Add(new Vector3(p.X, p.Y, p.Z));
            }
        }

        // 检查请求位置是否安全
        var candidate = new Vector3(x, y, z);
        if (IsClearOfVehicles(candidate, existingPositions, minDistance))
            return candidate;

        // 向外螺旋搜索（最多 16 次尝试）
        float step = minDistance;
        for (int ring = 1; ring <= 4; ring++)
        {
            float radius = step * ring;
            for (int dir = 0; dir < 4; dir++)
            {
                float offsetX = dir switch { 0 => radius, 1 => -radius, _ => 0 };
                float offsetZ = dir switch { 2 => radius, 3 => -radius, _ => 0 };
                float cx = x + offsetX;
                float cz = z + offsetZ;
                float cy = _terrain?.SampleHeight(cx, cz) ?? y;
                var test = new Vector3(cx, cy, cz);
                if (IsClearOfVehicles(test, existingPositions, minDistance))
                    return test;
            }
        }

        // 所有尝试都失败 — 强制使用偏移位置
        float fallbackX = x + minDistance * 2;
        float fallbackZ = z + minDistance * 2;
        float fallbackY = _terrain?.SampleHeight(fallbackX, fallbackZ) ?? y;
        return new Vector3(fallbackX, fallbackY, fallbackZ);
    }

    private static bool IsClearOfVehicles(Vector3 pos, List<Vector3> existing, float minDist)
    {
        float minDistSq = minDist * minDist;
        foreach (var other in existing)
        {
            float dx = pos.X - other.X;
            float dz = pos.Z - other.Z;
            if (dx * dx + dz * dz < minDistSq)
                return false;
        }
        return true;
    }
}
