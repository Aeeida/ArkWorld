using System.Numerics;
using GameLayer.Combat;
using GameLayer.Core;

namespace GameLayer.Vehicle;

/// <summary>
/// Server-authoritative vehicle management.
/// Mirrors Ark's IVehicleService — enter/exit, seat management, input relay.
/// </summary>
public sealed class VehicleManager
{
    private const byte VehicleFireFlag = 0x01;
    private const byte VehicleAscendFlag = 0x02;
    private const byte VehicleDescendFlag = 0x04;
    private const byte VehicleStrafeLeftFlag = 0x08;
    private const byte VehicleStrafeRightFlag = 0x10;

    private readonly EntityRegistry _entities;
    private readonly WeaponRegistry _weapons;
    private readonly Dictionary<int, VehicleState> _vehicles = [];
    private readonly Dictionary<int, VehicleDef> _defs = [];

    public event Action<int, System.Guid, int>? OnPlayerEnteredVehicle;  // vehicleId, playerId, seatIndex
    public event Action<int, System.Guid>? OnPlayerExitedVehicle;         // vehicleId, playerId

    public VehicleManager(EntityRegistry entities, WeaponRegistry weapons)
    {
        _entities = entities;
        _weapons = weapons;
        RegisterDefaultDefs();
    }

    /// <summary>
    /// 获取载具定义。
    /// </summary>
    public VehicleDef? GetDef(int vehicleDefId) =>
        _defs.TryGetValue(vehicleDefId, out var d) ? d : null;

    public VehicleState? GetVehicle(int vehicleEntityId) =>
        _vehicles.GetValueOrDefault(vehicleEntityId);

    public bool TryApplyControl(System.Guid playerId, int vehicleEntityId, float throttle, float steering, float brake, byte actionFlags, float turretYaw, float turretPitch, float deltaTime = 0.1f)
    {
        if (!_vehicles.TryGetValue(vehicleEntityId, out var vehicle))
            return false;

        var playerSeat = GetPlayerVehicle(playerId);
        if (playerSeat is null || playerSeat.Value.VehicleId != vehicleEntityId)
            return false;

        int seatIndex = playerSeat.Value.SeatIndex;
        var seatDef = vehicle.Definition.Seats[seatIndex];

        var entity = _entities.GetById(vehicleEntityId);
        if (entity is null)
            return false;

        bool updatedTurret = TryApplyTurretInput(vehicle, seatIndex, turretYaw, turretPitch);
        if (seatDef.Type != SeatType.Driver)
            return updatedTurret;

        throttle = Math.Clamp(throttle, -1f, 1f);
        steering = Math.Clamp(steering, -1f, 1f);
        brake = Math.Clamp(brake, 0f, 1f);

        var rotation = entity.Rotation;
        var yawDelta = steering * vehicle.Definition.TurnSpeed * deltaTime;
        rotation = Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.UnitY, yawDelta) * rotation);

        var forward = Vector3.Transform(-Vector3.UnitZ, rotation);
        if (forward.LengthSquared() > 0.0001f)
            forward = Vector3.Normalize(new Vector3(forward.X, 0f, forward.Z));
        else
            forward = -Vector3.UnitZ;

        var currentSpeed = Vector3.Dot(entity.Velocity, forward);
        var targetSpeed = throttle >= 0
            ? throttle * vehicle.Definition.MaxSpeed
            : throttle * vehicle.Definition.MaxSpeed * 0.8f;

        if (brake > 0)
            currentSpeed = MathF.Max(0f, currentSpeed - vehicle.Definition.Acceleration * (1f + brake) * deltaTime);
        else
            currentSpeed += (targetSpeed - currentSpeed) * MathF.Min(1f, vehicle.Definition.Acceleration * deltaTime);

        var velocity = forward * currentSpeed;
        if (vehicle.Definition.Type == VehicleType.Plane)
            velocity = ApplyAircraftControl(vehicle.Definition, rotation, velocity, actionFlags);

        if (vehicle.Definition.MaxFuel > 0f && float.IsFinite(vehicle.Definition.MaxFuel))
        {
            var fuelBurn = MathF.Abs(throttle) * vehicle.Definition.FuelConsumption * deltaTime * 10f;
            vehicle.Fuel = MathF.Max(0f, vehicle.Fuel - fuelBurn);
        }

        entity.Rotation = rotation;
        entity.Velocity = velocity;
        entity.FuelPercent = vehicle.Definition.MaxFuel > 0f && float.IsFinite(vehicle.Definition.MaxFuel)
            ? vehicle.Fuel / vehicle.Definition.MaxFuel * 100f
            : 100f;
        entity.TurretYaw = vehicle.TurretYaw;
        entity.TurretPitch = vehicle.TurretPitch;
        return true;
    }

    private static bool TryApplyTurretInput(VehicleState vehicle, int seatIndex, float turretYaw, float turretPitch)
    {
        var seatDef = vehicle.Definition.Seats[seatIndex];
        if (!seatDef.HasWeapon)
            return false;

        if (!float.IsFinite(turretYaw) || !float.IsFinite(turretPitch))
            return false;

        var seatRuntime = vehicle.SeatRuntime[seatIndex];
        seatRuntime.TurretYaw = turretYaw;
        seatRuntime.TurretPitch = Math.Clamp(turretPitch, -1.2f, 1.0f);
        if (ResolvePrimaryMountedSeatIndex(vehicle.Definition) == seatIndex)
        {
            vehicle.TurretYaw = seatRuntime.TurretYaw;
            vehicle.TurretPitch = seatRuntime.TurretPitch;
        }
        return true;
    }

    private static int ResolvePrimaryMountedSeatIndex(VehicleDef def)
    {
        for (int i = 0; i < def.Seats.Length; i++)
        {
            if (def.Seats[i].HasWeapon)
                return i;
        }

        return -1;
    }

    private static Vector3 ApplyAircraftControl(VehicleDef definition, Quaternion rotation, Vector3 velocity, byte actionFlags)
    {
        const float aircraftLiftFactor = 0.75f;
        const float aircraftStrafeFactor = 0.6f;

        float verticalInput = 0f;
        if ((actionFlags & VehicleAscendFlag) != 0)
            verticalInput += 1f;
        if ((actionFlags & VehicleDescendFlag) != 0)
            verticalInput -= 1f;

        float strafeInput = 0f;
        if ((actionFlags & VehicleStrafeLeftFlag) != 0)
            strafeInput -= 1f;
        if ((actionFlags & VehicleStrafeRightFlag) != 0)
            strafeInput += 1f;

        var right = Vector3.Transform(Vector3.UnitX, rotation);
        if (right.LengthSquared() > 0.0001f)
            right = Vector3.Normalize(new Vector3(right.X, 0f, right.Z));
        else
            right = Vector3.UnitX;

        return velocity
            + Vector3.UnitY * (verticalInput * definition.MaxSpeed * aircraftLiftFactor)
            + right * (strafeInput * definition.MaxSpeed * aircraftStrafeFactor);
    }

    /// <summary>
    /// 注册默认载具定义（与客户端 VehicleDefRegistry 保持一致）。
    /// </summary>
    private void RegisterDefaultDefs()
    {
        RegisterDef(new VehicleDef(
            Id: 1, Name: "越野车", Type: VehicleType.Car,
            MaxHealth: 500f, MaxSpeed: 25f, Acceleration: 8f,
            TurnSpeed: 3f, MaxFuel: 100f, FuelConsumption: 0.05f,
            Seats: [
                new(0, SeatType.Driver, new(0, 0.5f, 0.5f), false, 0),
                new(1, SeatType.Passenger, new(0.8f, 0.5f, 0.5f), false, 0),
                new(2, SeatType.Passenger, new(-0.8f, 0.5f, -0.5f), false, 0),
                new(3, SeatType.Passenger, new(0.8f, 0.5f, -0.5f), false, 0),
            ]));

        RegisterDef(new VehicleDef(
            Id: 2, Name: "主战坦克", Type: VehicleType.Tank,
            MaxHealth: 3000f, MaxSpeed: 12f, Acceleration: 3f,
            TurnSpeed: 1.5f, MaxFuel: 300f, FuelConsumption: 0.2f,
            Seats: [
                new(0, SeatType.Driver, new(0, 1f, 1f), false, 0),
                new(1, SeatType.Gunner, new(0, 2f, 0), true, 60),
                new(2, SeatType.Gunner, new(0, 2.5f, -0.5f), true, 61),
            ]));

        RegisterDef(new VehicleDef(
            Id: 3, Name: "防空炮", Type: VehicleType.AntiAir,
            MaxHealth: 800f, MaxSpeed: 0f, Acceleration: 0f,
            TurnSpeed: 0f, MaxFuel: float.MaxValue, FuelConsumption: 0f,
            Seats: [
                new(0, SeatType.Gunner, new(0, 1.5f, 0), true, 61),
            ]));

        RegisterDef(new VehicleDef(
            Id: 4, Name: "战斗机", Type: VehicleType.Plane,
            MaxHealth: 1000f, MaxSpeed: 80f, Acceleration: 15f,
            TurnSpeed: 2f, MaxFuel: 200f, FuelConsumption: 0.3f,
            Seats: [
                new(0, SeatType.Driver, new(0, 0, 1f), true, 61),
            ]));

        RegisterDef(new VehicleDef(
            Id: 5, Name: "巡逻艇", Type: VehicleType.Boat,
            MaxHealth: 1500f, MaxSpeed: 15f, Acceleration: 4f,
            TurnSpeed: 1.8f, MaxFuel: 250f, FuelConsumption: 0.15f,
            Seats: [
                new(0, SeatType.Driver, new(0, 1.5f, 2f), false, 0),
                new(1, SeatType.Gunner, new(0, 2f, 0), true, 61),
                new(2, SeatType.Passenger, new(-1f, 1f, -1f), false, 0),
                new(3, SeatType.Passenger, new(1f, 1f, -1f), false, 0),
            ]));

        RegisterDef(new VehicleDef(
            Id: 6, Name: "运载火箭", Type: VehicleType.Rocket,
            MaxHealth: 2000f, MaxSpeed: 100f, Acceleration: 20f,
            TurnSpeed: 0.5f, MaxFuel: 500f, FuelConsumption: 1f,
            Seats: [
                new(0, SeatType.Driver, new(0, 2f, 0), false, 0),
                new(1, SeatType.Passenger, new(0.5f, 2f, 0), false, 0),
            ]));
    }

    private void RegisterDef(VehicleDef def) => _defs[def.Id] = def;

    /// <summary>
    /// Registers a vehicle entity with seat definitions.
    /// </summary>
    public void RegisterVehicle(int entityId, VehicleDef def)
    {
        _vehicles[entityId] = new VehicleState
        {
            EntityId = entityId,
            Definition = def,
            Seats = new Guid?[def.Seats.Length],
            SeatRuntime = CreateSeatRuntime(def),
            Health = def.MaxHealth,
            Fuel = def.MaxFuel
        };
    }

    private VehicleSeatRuntime[] CreateSeatRuntime(VehicleDef def)
    {
        var result = new VehicleSeatRuntime[def.Seats.Length];
        for (int i = 0; i < def.Seats.Length; i++)
        {
            var seat = def.Seats[i];
            int magCapacity = 0;
            int maxReserve = 0;
            if (seat.HasWeapon && seat.WeaponDefId > 0 && _weapons.Get(seat.WeaponDefId) is { } weaponDef)
            {
                magCapacity = weaponDef.MagCapacity;
                maxReserve = weaponDef.MaxReserve;
            }

            result[i] = new VehicleSeatRuntime
            {
                WeaponDefId = seat.WeaponDefId,
                CurrentMag = magCapacity,
                MagCapacity = magCapacity,
                ReserveAmmo = maxReserve,
                MaxReserve = maxReserve,
                TurretYaw = 0f,
                TurretPitch = 0f,
                OperationProgress = 0f,
                SkillScalar = 1f,
                RepairStep = 0,
                RepairStepCount = 0,
                MaterialUnits = 0,
            };
        }

        return result;
    }

    public VehicleSeatRuntime? GetSeatRuntime(int vehicleEntityId, int seatIndex)
    {
        if (!_vehicles.TryGetValue(vehicleEntityId, out var vehicle))
            return null;
        if (seatIndex < 0 || seatIndex >= vehicle.SeatRuntime.Length)
            return null;

        UpdateSeatRuntimeTelemetry(vehicle.SeatRuntime[seatIndex]);
        return vehicle.SeatRuntime[seatIndex];
    }

    public bool TryConsumeMountedShot(int vehicleEntityId, int seatIndex, WeaponDef weaponDef, out VehicleSeatRuntime? seatRuntime)
    {
        seatRuntime = null;
        if (!_vehicles.TryGetValue(vehicleEntityId, out var vehicle))
            return false;
        if (seatIndex < 0 || seatIndex >= vehicle.SeatRuntime.Length)
            return false;

        var runtime = vehicle.SeatRuntime[seatIndex];
        UpdateSeatRuntimeTelemetry(runtime);
        if (runtime.CurrentMag <= 0 || runtime.FireCycleRemaining > 0f || runtime.Heat >= 0.995f || runtime.ReloadRemaining > 0f || runtime.FaultRemaining > 0f || runtime.MaintenanceRemaining > 0f)
            return false;

        runtime.CurrentMag = Math.Max(0, runtime.CurrentMag - 1);
        runtime.FireCycleRemaining = weaponDef.FireInterval;
        runtime.Heat = MathF.Min(1f, runtime.Heat + runtime.HeatPerShot);
        runtime.MaintenanceLevel = MathF.Max(0.25f, runtime.MaintenanceLevel - 0.012f - runtime.Heat * 0.01f);
        if (runtime.Heat >= 0.995f)
        {
            runtime.FaultCode = 2;
            runtime.FaultRemaining = MathF.Max(runtime.FaultRemaining, 1.25f + runtime.Heat * 1.5f);
        }
        else if (runtime.MaintenanceLevel < 0.36f && Random.Shared.NextSingle() < (0.42f - runtime.MaintenanceLevel) * 0.22f)
        {
            runtime.FaultCode = 3;
            runtime.FaultRemaining = MathF.Max(runtime.FaultRemaining, 0.95f);
        }
        else if (runtime.MaintenanceLevel < 0.55f && runtime.Heat > 0.48f && Random.Shared.NextSingle() < (0.62f - runtime.MaintenanceLevel) * 0.16f)
        {
            runtime.FaultCode = 4;
            runtime.FaultRemaining = MathF.Max(runtime.FaultRemaining, 0.78f);
        }
        else if (runtime.Heat > 0.72f && Random.Shared.NextSingle() < (runtime.Heat - 0.72f) * (0.12f + (1f - runtime.MaintenanceLevel) * 0.24f))
        {
            runtime.FaultCode = 1;
            runtime.FaultRemaining = MathF.Max(runtime.FaultRemaining, 0.85f);
        }
        runtime.RuntimeUpdatedAtUtc = DateTime.UtcNow;
        seatRuntime = runtime;
        return true;
    }

    public void RefreshMountedWeaponState(int vehicleEntityId, int seatIndex)
    {
        if (!_vehicles.TryGetValue(vehicleEntityId, out var vehicle))
            return;
        if (seatIndex < 0 || seatIndex >= vehicle.SeatRuntime.Length)
            return;

        UpdateSeatRuntimeTelemetry(vehicle.SeatRuntime[seatIndex]);
    }

    public bool TryStartMountedReload(int vehicleEntityId, int seatIndex, WeaponDef weaponDef, out VehicleSeatRuntime? seatRuntime)
    {
        seatRuntime = null;
        if (!_vehicles.TryGetValue(vehicleEntityId, out var vehicle))
            return false;
        if (seatIndex < 0 || seatIndex >= vehicle.SeatRuntime.Length)
            return false;

        var runtime = vehicle.SeatRuntime[seatIndex];
        UpdateSeatRuntimeTelemetry(runtime);
        if (runtime.ReloadRemaining > 0f || runtime.CurrentMag >= runtime.MagCapacity)
            return false;

        runtime.ReloadRemaining = weaponDef.ReloadTime;
        runtime.FaultCode = 0;
        runtime.FaultRemaining = 0f;
        runtime.OperationProgress = 0f;
        runtime.RepairStep = 0;
        runtime.RepairStepCount = 0;
        runtime.MaterialUnits = 0;
        runtime.SkillScalar = 1f;
        runtime.RuntimeUpdatedAtUtc = DateTime.UtcNow;
        seatRuntime = runtime;
        return true;
    }

    public bool TryClearMountedFault(int vehicleEntityId, int seatIndex, float skillScalar, int materialUnits, out VehicleSeatRuntime? seatRuntime)
    {
        seatRuntime = null;
        if (!_vehicles.TryGetValue(vehicleEntityId, out var vehicle))
            return false;
        if (seatIndex < 0 || seatIndex >= vehicle.SeatRuntime.Length)
            return false;

        var runtime = vehicle.SeatRuntime[seatIndex];
        UpdateSeatRuntimeTelemetry(runtime);
        if (runtime.FaultCode == 0 || runtime.ReloadRemaining > 0f || runtime.MaintenanceRemaining > 0f)
            return false;

        runtime.FaultRemaining = runtime.FaultCode switch
        {
            2 => 0.65f / MathF.Max(0.75f, skillScalar),
            3 => 0.58f / MathF.Max(0.75f, skillScalar),
            4 => 0.52f / MathF.Max(0.75f, skillScalar),
            _ => 0.45f / MathF.Max(0.75f, skillScalar),
        };
        runtime.OperationTotalDuration = runtime.FaultRemaining;
        runtime.OperationProgress = 0f;
        runtime.SkillScalar = skillScalar;
        runtime.RepairStep = 1;
        runtime.RepairStepCount = (byte)(runtime.FaultCode == 2 ? 3 : 2);
        runtime.MaterialUnits = (byte)Math.Clamp(materialUnits, 0, byte.MaxValue);
        runtime.RuntimeUpdatedAtUtc = DateTime.UtcNow;
        seatRuntime = runtime;
        return true;
    }

    public bool TryMaintainMountedWeapon(int vehicleEntityId, int seatIndex, float skillScalar, int materialUnits, out VehicleSeatRuntime? seatRuntime)
    {
        seatRuntime = null;
        if (!_vehicles.TryGetValue(vehicleEntityId, out var vehicle))
            return false;
        if (seatIndex < 0 || seatIndex >= vehicle.SeatRuntime.Length)
            return false;

        var runtime = vehicle.SeatRuntime[seatIndex];
        UpdateSeatRuntimeTelemetry(runtime);
        if (runtime.ReloadRemaining > 0f || runtime.MaintenanceRemaining > 0f || runtime.FaultRemaining > 0f)
            return false;

        runtime.MaintenanceRemaining = 1.6f / MathF.Max(0.75f, skillScalar);
        runtime.MaintenanceLevel = MathF.Max(0f, runtime.MaintenanceLevel - 0.08f) + 0.08f;
        runtime.OperationTotalDuration = runtime.MaintenanceRemaining;
        runtime.OperationProgress = 0f;
        runtime.SkillScalar = skillScalar;
        runtime.RepairStep = 1;
        runtime.RepairStepCount = 3;
        runtime.MaterialUnits = (byte)Math.Clamp(materialUnits, 0, byte.MaxValue);
        runtime.RuntimeUpdatedAtUtc = DateTime.UtcNow;
        seatRuntime = runtime;
        return true;
    }

    private static void UpdateSeatRuntimeTelemetry(VehicleSeatRuntime runtime)
    {
        var now = DateTime.UtcNow;
        var elapsed = (float)(now - runtime.RuntimeUpdatedAtUtc).TotalSeconds;
        if (elapsed <= 0f)
            return;

        runtime.FireCycleRemaining = MathF.Max(0f, runtime.FireCycleRemaining - elapsed);
        float coolingRate = runtime.HeatCooldownPerSecond * (0.45f + (1f - runtime.Heat) * 1.35f);
        runtime.Heat = MathF.Max(0f, runtime.Heat - elapsed * coolingRate);
        if (runtime.FaultRemaining > 0f)
        {
            runtime.FaultRemaining = MathF.Max(0f, runtime.FaultRemaining - elapsed);
            UpdateRepairProgress(runtime, runtime.FaultRemaining);
            if (runtime.FaultRemaining <= 0f)
            {
                runtime.FaultCode = 0;
                runtime.OperationProgress = 0f;
                runtime.RepairStep = 0;
                runtime.RepairStepCount = 0;
                runtime.MaterialUnits = 0;
                runtime.SkillScalar = 1f;
                runtime.OperationTotalDuration = 0f;
            }
        }
        if (runtime.MaintenanceRemaining > 0f)
        {
            runtime.MaintenanceRemaining = MathF.Max(0f, runtime.MaintenanceRemaining - elapsed);
            runtime.MaintenanceLevel = MathF.Min(1f, runtime.MaintenanceLevel + elapsed * 0.32f * MathF.Max(1f, runtime.SkillScalar));
            runtime.Heat = MathF.Max(0f, runtime.Heat - elapsed * runtime.HeatCooldownPerSecond * 0.45f);
            UpdateRepairProgress(runtime, runtime.MaintenanceRemaining);
            if (runtime.MaintenanceRemaining <= 0f)
            {
                runtime.OperationProgress = 0f;
                runtime.RepairStep = 0;
                runtime.RepairStepCount = 0;
                runtime.MaterialUnits = 0;
                runtime.SkillScalar = 1f;
                runtime.OperationTotalDuration = 0f;
            }
        }
        if (runtime.ReloadRemaining > 0f)
        {
            runtime.ReloadRemaining = MathF.Max(0f, runtime.ReloadRemaining - elapsed);
            if (runtime.ReloadRemaining <= 0f)
            {
                int ammoNeeded = Math.Max(0, runtime.MagCapacity - runtime.CurrentMag);
                int ammoToLoad = runtime.MaxReserve > 0 ? Math.Min(ammoNeeded, runtime.ReserveAmmo) : ammoNeeded;
                runtime.CurrentMag += ammoToLoad;
                runtime.ReserveAmmo = Math.Max(0, runtime.ReserveAmmo - ammoToLoad);
            }
        }
        runtime.RuntimeUpdatedAtUtc = now;
    }

    private static void UpdateRepairProgress(VehicleSeatRuntime runtime, float remaining)
    {
        if (runtime.OperationTotalDuration <= 0.0001f || runtime.RepairStepCount == 0)
            return;

        runtime.OperationProgress = Math.Clamp(1f - remaining / runtime.OperationTotalDuration, 0f, 1f);
        runtime.RepairStep = (byte)Math.Clamp((int)MathF.Floor(runtime.OperationProgress * runtime.RepairStepCount) + 1, 1, runtime.RepairStepCount);
    }

    public bool TryUpdateMountedAim(System.Guid playerId, int vehicleEntityId, Vector3 worldDirection)
    {
        if (!_vehicles.TryGetValue(vehicleEntityId, out var vehicle))
            return false;

        var playerSeat = GetPlayerVehicle(playerId);
        if (playerSeat is null || playerSeat.Value.VehicleId != vehicleEntityId)
            return false;

        int seatIndex = playerSeat.Value.SeatIndex;
        var seatDef = vehicle.Definition.Seats[seatIndex];
        if (!seatDef.HasWeapon)
            return false;

        var vehicleEntity = _entities.GetById(vehicleEntityId);
        if (vehicleEntity is null)
            return false;

        if (worldDirection.LengthSquared() <= 1e-6f)
            return false;

        worldDirection = Vector3.Normalize(worldDirection);
        var inverseRotation = Quaternion.Conjugate(vehicleEntity.Rotation);
        var localDirection = Vector3.Transform(worldDirection, inverseRotation);
        if (localDirection.LengthSquared() <= 1e-6f)
            return false;

        localDirection = Vector3.Normalize(localDirection);
        float turretYaw = MathF.Atan2(localDirection.X, -localDirection.Z);
        float turretPitch = MathF.Atan2(localDirection.Y, MathF.Sqrt(localDirection.X * localDirection.X + localDirection.Z * localDirection.Z));
        TryApplyTurretInput(vehicle, seatIndex, turretYaw, turretPitch);
        return true;
    }

    public void SyncSnapshotState(ServerWorldState world)
    {
        foreach (var (vehicleId, vehicle) in _vehicles)
        {
            var vehicleEntity = world.Entities.GetById(vehicleId);
            if (vehicleEntity is null)
                continue;

            ushort occupiedSeatCount = 0;
            for (int i = 0; i < vehicle.Seats.Length; i++)
            {
                if (vehicle.Seats[i].HasValue)
                    occupiedSeatCount++;
            }

            vehicleEntity.SeatCount = (ushort)vehicle.Definition.Seats.Length;
            vehicleEntity.OccupiedSeatCount = occupiedSeatCount;
            vehicleEntity.FuelPercent = vehicle.Definition.MaxFuel > 0f && float.IsFinite(vehicle.Definition.MaxFuel)
                ? vehicle.Fuel / vehicle.Definition.MaxFuel * 100f
                : 100f;
            vehicleEntity.TurretYaw = vehicle.TurretYaw;
            vehicleEntity.TurretPitch = vehicle.TurretPitch;

            for (int seatIndex = 0; seatIndex < vehicle.Seats.Length; seatIndex++)
            {
                var occupantId = vehicle.Seats[seatIndex];
                if (!occupantId.HasValue)
                    continue;

                var occupantEntity = world.Entities.GetByNetworkId(occupantId.Value);
                if (occupantEntity is null)
                    continue;

                var seatDef = vehicle.Definition.Seats[seatIndex];
                var worldOffset = Vector3.Transform(seatDef.LocalOffset, vehicleEntity.Rotation);
                occupantEntity.AttachedVehicleEntityId = vehicleId;
                occupantEntity.SeatIndex = (byte)seatIndex;
                occupantEntity.SeatType = (byte)seatDef.Type;
                occupantEntity.HasMountedWeapon = seatDef.HasWeapon;
                occupantEntity.SeatOffset = seatDef.LocalOffset;
                occupantEntity.Position = vehicleEntity.Position + worldOffset;
                occupantEntity.Rotation = vehicleEntity.Rotation;
                occupantEntity.Velocity = vehicleEntity.Velocity;
                occupantEntity.ZoneId = vehicleEntity.ZoneId;
                occupantEntity.TurretYaw = vehicle.SeatRuntime[seatIndex].TurretYaw;
                occupantEntity.TurretPitch = vehicle.SeatRuntime[seatIndex].TurretPitch;
                occupantEntity.MountedWeaponHeat = vehicle.SeatRuntime[seatIndex].Heat;
                occupantEntity.MountedWeaponCycleRemaining = vehicle.SeatRuntime[seatIndex].FireCycleRemaining;
                occupantEntity.MountedWeaponReloadRemaining = vehicle.SeatRuntime[seatIndex].ReloadRemaining;
                occupantEntity.MountedWeaponFaultRemaining = vehicle.SeatRuntime[seatIndex].FaultRemaining;
                occupantEntity.MountedWeaponMaintenanceRemaining = vehicle.SeatRuntime[seatIndex].MaintenanceRemaining;
                occupantEntity.MountedWeaponMaintenanceLevel = vehicle.SeatRuntime[seatIndex].MaintenanceLevel;
                occupantEntity.MountedWeaponOperationProgress = vehicle.SeatRuntime[seatIndex].OperationProgress;
                occupantEntity.MountedWeaponSkillScalar = vehicle.SeatRuntime[seatIndex].SkillScalar;
                occupantEntity.MountedWeaponFaultCode = vehicle.SeatRuntime[seatIndex].FaultCode;
                occupantEntity.MountedWeaponRepairStep = vehicle.SeatRuntime[seatIndex].RepairStep;
                occupantEntity.MountedWeaponRepairStepCount = vehicle.SeatRuntime[seatIndex].RepairStepCount;
                occupantEntity.MountedWeaponMaterialUnits = vehicle.SeatRuntime[seatIndex].MaterialUnits;

                if (seatDef.HasWeapon && seatDef.WeaponDefId > 0 && vehicle.SeatRuntime[seatIndex] is { } seatRuntime)
                {
                    _weapons.TryApplyLoadout(occupantEntity, seatDef.WeaponDefId, seatRuntime.CurrentMag, seatRuntime.ReserveAmmo, setPersonal: false);
                    occupantEntity.IsReloading = seatRuntime.ReloadRemaining > 0f;
                }
                else
                {
                    _weapons.RestorePersonalLoadout(occupantEntity);
                    occupantEntity.IsReloading = false;
                }
            }
        }
    }

    public bool EnterVehicle(Guid playerId, int vehicleEntityId, int preferredSeat = 0)
    {
        if (!_vehicles.TryGetValue(vehicleEntityId, out var vehicle))
            return false;

        // Already in a vehicle?
        if (GetPlayerVehicle(playerId) is not null)
            return false;

        bool vehicleWasEmpty = true;
        for (int i = 0; i < vehicle.Seats.Length; i++)
        {
            if (vehicle.Seats[i] is not null)
            {
                vehicleWasEmpty = false;
                break;
            }
        }

        // Find seat (empty vehicle always prefers the first driver seat)
        int seatIndex = -1;
        if (vehicleWasEmpty)
        {
            for (int i = 0; i < vehicle.Definition.Seats.Length; i++)
            {
                if (vehicle.Definition.Seats[i].Type == SeatType.Driver && vehicle.Seats[i] is null)
                {
                    seatIndex = i;
                    break;
                }
            }
        }

        if (seatIndex < 0 && preferredSeat >= 0 && preferredSeat < vehicle.Seats.Length && vehicle.Seats[preferredSeat] is null)
            seatIndex = preferredSeat;

        if (seatIndex < 0)
        {
            for (int i = 0; i < vehicle.Seats.Length; i++)
            {
                if (vehicle.Seats[i] is null) { seatIndex = i; break; }
            }
        }

        if (seatIndex < 0) return false; // No empty seats

        vehicle.Seats[seatIndex] = playerId;
        OnPlayerEnteredVehicle?.Invoke(vehicleEntityId, playerId, seatIndex);
        return true;
    }

    public bool ExitVehicle(Guid playerId)
    {
        foreach (var (vehicleId, vehicle) in _vehicles)
        {
            for (int i = 0; i < vehicle.Seats.Length; i++)
            {
                if (vehicle.Seats[i] == playerId)
                {
                    vehicle.Seats[i] = null;
                    ClearVehicleAttachment(playerId);
                    OnPlayerExitedVehicle?.Invoke(vehicleId, playerId);
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Force a player out of any vehicle they occupy — called on disconnect/session cleanup.
    /// </summary>
    public void ForceExitPlayer(Guid playerId)
    {
        foreach (var (_, vehicle) in _vehicles)
        {
            for (int i = 0; i < vehicle.Seats.Length; i++)
            {
                if (vehicle.Seats[i] == playerId)
                {
                    vehicle.Seats[i] = null;
                    ClearVehicleAttachment(playerId);
                }
            }
        }
    }

    public bool SwitchSeat(Guid playerId, int targetSeat)
    {
        foreach (var (_, vehicle) in _vehicles)
        {
            for (int i = 0; i < vehicle.Seats.Length; i++)
            {
                if (vehicle.Seats[i] == playerId)
                {
                    if (targetSeat < 0 || targetSeat >= vehicle.Seats.Length) return false;
                    if (vehicle.Seats[targetSeat] is not null) return false;

                    vehicle.Seats[i] = null;
                    vehicle.Seats[targetSeat] = playerId;
                    return true;
                }
            }
        }
        return false;
    }

    public (int VehicleId, int SeatIndex)? GetPlayerVehicle(System.Guid playerId)
    {
        foreach (var (vehicleId, vehicle) in _vehicles)
        {
            for (int i = 0; i < vehicle.Seats.Length; i++)
            {
                if (vehicle.Seats[i] == playerId)
                    return (vehicleId, i);
            }
        }
        return null;
    }

    public IEnumerable<int> GetNearbyVehicles(Vector3 position, float radius = 10f)
    {
        var radiusSq = radius * radius;
        foreach (var (vehicleId, _) in _vehicles)
        {
            var entity = _entities.GetById(vehicleId);
            if (entity is not null)
            {
                var diff = entity.Position - position;
                if (diff.LengthSquared() <= radiusSq)
                    yield return vehicleId;
            }
        }
    }

    private void ClearVehicleAttachment(System.Guid playerId)
    {
        var entity = _entities.GetByNetworkId(playerId);
        if (entity is null)
            return;

        entity.AttachedVehicleEntityId = 0;
        entity.SeatIndex = 0;
        entity.SeatType = 0;
        entity.HasMountedWeapon = false;
        entity.SeatOffset = Vector3.Zero;
        entity.TurretYaw = 0f;
        entity.TurretPitch = 0f;
        entity.MountedWeaponHeat = 0f;
        entity.MountedWeaponCycleRemaining = 0f;
        entity.MountedWeaponReloadRemaining = 0f;
        entity.MountedWeaponFaultRemaining = 0f;
        entity.MountedWeaponMaintenanceRemaining = 0f;
        entity.MountedWeaponMaintenanceLevel = 0f;
        entity.MountedWeaponOperationProgress = 0f;
        entity.MountedWeaponSkillScalar = 1f;
        entity.MountedWeaponFaultCode = 0;
        entity.MountedWeaponRepairStep = 0;
        entity.MountedWeaponRepairStepCount = 0;
        entity.MountedWeaponMaterialUnits = 0;
        _weapons.RestorePersonalLoadout(entity);
    }
}

public sealed class VehicleState
{
    public int EntityId { get; init; }
    public VehicleDef Definition { get; init; }
    public Guid?[] Seats { get; init; } = [];
    public VehicleSeatRuntime[] SeatRuntime { get; init; } = [];
    public float Health { get; set; }
    public float Fuel { get; set; }
    public float TurretYaw { get; set; }
    public float TurretPitch { get; set; }
}

public sealed class VehicleSeatRuntime
{
    public int WeaponDefId { get; init; }
    public int CurrentMag { get; set; }
    public int MagCapacity { get; init; }
    public int ReserveAmmo { get; set; }
    public int MaxReserve { get; init; }
    public float TurretYaw { get; set; }
    public float TurretPitch { get; set; }
    public float Heat { get; set; }
    public float FireCycleRemaining { get; set; }
    public float HeatPerShot { get; init; }
    public float HeatCooldownPerSecond { get; init; }
    public float ReloadRemaining { get; set; }
    public float MaintenanceRemaining { get; set; }
    public float MaintenanceLevel { get; set; }
    public float OperationTotalDuration { get; set; }
    public float OperationProgress { get; set; }
    public float SkillScalar { get; set; }
    public byte FaultCode { get; set; }
    public byte RepairStep { get; set; }
    public byte RepairStepCount { get; set; }
    public byte MaterialUnits { get; set; }
    public float FaultRemaining { get; set; }
    public DateTime RuntimeUpdatedAtUtc { get; set; }
}

public record struct VehicleDef(
    int Id, string Name, VehicleType Type,
    float MaxHealth, float MaxSpeed, float Acceleration,
    float TurnSpeed, float MaxFuel, float FuelConsumption,
    VehicleSeatDef[] Seats);

public record struct VehicleSeatDef(int Index, SeatType Type, Vector3 LocalOffset, bool HasWeapon, int WeaponDefId);

public enum VehicleType : byte { Car = 0, Tank = 1, AntiAir = 2, Plane = 3, Boat = 4, Rocket = 5, Helicopter = 6 }
public enum SeatType : byte { Driver = 0, Gunner = 1, Passenger = 2 }
