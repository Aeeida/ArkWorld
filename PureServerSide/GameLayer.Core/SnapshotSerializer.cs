using System.Buffers.Binary;
using System.Numerics;

namespace GameLayer.Core;

/// <summary>
/// Serializes/deserializes entity state snapshots for network transmission.
/// Wire format per entity:
/// [4:entityId][16:networkId][1:type][4:typeId]
/// [12:posXYZ][16:rotXYZW][12:velXYZ]
/// [4:health][4:maxHealth]
/// [4:weaponDefId][4:currentMag][4:magCapacity][4:reserveAmmo][4:maxReserve]
/// [4:attachedVehicleId][12:seatOffsetXYZ]
/// [4:fuelPercent][4:turretYaw][4:turretPitch][4:mountedWeaponHeat][4:mountedWeaponCycle][4:mountedWeaponReload][4:mountedWeaponFault][4:mountedWeaponMaintenance][4:mountedWeaponMaintenanceLevel][4:mountedWeaponOperationProgress][4:mountedWeaponSkillScalar][4:altitude][4:orbitalVelocity][4:remainingDeltaV]
/// [2:seatCount][2:occupiedSeatCount]
/// [1:weaponCategory][1:seatIndex][1:seatType][1:spaceFlightPhase][1:buildingLevel][1:buildingConstructionProgress][1:frontDamage][1:backDamage][1:rightDamage][1:leftDamage]
/// [12:cluster0XYZ][4:cluster0Strength][4:cluster0Age][4:cluster0RepairFill][12:cluster1XYZ][4:cluster1Strength][4:cluster1Age][4:cluster1RepairFill]
/// [12:cluster2XYZ][4:cluster2Strength][4:cluster2Age][4:cluster2RepairFill][1:buildingDamageLayerState][1:mountedWeaponFaultCode][1:mountedWeaponRepairStep][1:mountedWeaponRepairStepCount][1:mountedWeaponMaterialUnits][1:flags]
/// Total: 258 bytes per entity.
/// This matches what Ark's ServerSnapshot.EntityStates expects.
/// </summary>
public static class SnapshotSerializer
{
    public const int BytesPerEntity = 258;

    /// <summary>
    /// Serializes a collection of entities into a binary snapshot buffer.
    /// </summary>
    public static byte[] Serialize(IReadOnlyCollection<ServerEntity> entities)
    {
        var buffer = new byte[4 + entities.Count * BytesPerEntity]; // 4-byte entity count header
        var span = buffer.AsSpan();

        BinaryPrimitives.WriteInt32LittleEndian(span, entities.Count);
        var offset = 4;

        foreach (var e in entities)
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[offset..], e.Id);
            offset += 4;

            e.NetworkId.TryWriteBytes(span.Slice(offset, 16));
            offset += 16;

            span[offset++] = (byte)e.Type;

            BinaryPrimitives.WriteInt32LittleEndian(span[offset..], e.TypeId);
            offset += 4;

            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.Position.X); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.Position.Y); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.Position.Z); offset += 4;

            var rotation = NormalizeQuaternion(e.Rotation);
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], rotation.X); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], rotation.Y); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], rotation.Z); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], rotation.W); offset += 4;

            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.Velocity.X); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.Velocity.Y); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.Velocity.Z); offset += 4;

            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.Health); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.MaxHealth); offset += 4;

            BinaryPrimitives.WriteInt32LittleEndian(span[offset..], e.WeaponDefId); offset += 4;
            BinaryPrimitives.WriteInt32LittleEndian(span[offset..], e.CurrentMag); offset += 4;
            BinaryPrimitives.WriteInt32LittleEndian(span[offset..], e.MagCapacity); offset += 4;
            BinaryPrimitives.WriteInt32LittleEndian(span[offset..], e.ReserveAmmo); offset += 4;
            BinaryPrimitives.WriteInt32LittleEndian(span[offset..], e.MaxReserve); offset += 4;

            BinaryPrimitives.WriteInt32LittleEndian(span[offset..], e.AttachedVehicleEntityId); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.SeatOffset.X); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.SeatOffset.Y); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.SeatOffset.Z); offset += 4;

            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.FuelPercent); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.TurretYaw); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.TurretPitch); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.MountedWeaponHeat); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.MountedWeaponCycleRemaining); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.MountedWeaponReloadRemaining); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.MountedWeaponFaultRemaining); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.MountedWeaponMaintenanceRemaining); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.MountedWeaponMaintenanceLevel); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.MountedWeaponOperationProgress); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.MountedWeaponSkillScalar); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.Altitude); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.OrbitalVelocity); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.RemainingDeltaV); offset += 4;

            BinaryPrimitives.WriteUInt16LittleEndian(span[offset..], e.SeatCount); offset += 2;
            BinaryPrimitives.WriteUInt16LittleEndian(span[offset..], e.OccupiedSeatCount); offset += 2;

            span[offset++] = e.WeaponCategory;
            span[offset++] = e.SeatIndex;
            span[offset++] = e.SeatType;
            span[offset++] = e.SpaceFlightPhase;
            span[offset++] = e.BuildingLevel;
            span[offset++] = e.BuildingConstructionProgress;
            span[offset++] = e.BuildingFrontDamage;
            span[offset++] = e.BuildingBackDamage;
            span[offset++] = e.BuildingRightDamage;
            span[offset++] = e.BuildingLeftDamage;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.BuildingDamageCluster0X); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.BuildingDamageCluster0Y); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.BuildingDamageCluster0Z); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.BuildingDamageCluster0Strength); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.BuildingDamageCluster0Age); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.BuildingDamageCluster0RepairFill); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.BuildingDamageCluster1X); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.BuildingDamageCluster1Y); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.BuildingDamageCluster1Z); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.BuildingDamageCluster1Strength); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.BuildingDamageCluster1Age); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.BuildingDamageCluster1RepairFill); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.BuildingDamageCluster2X); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.BuildingDamageCluster2Y); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.BuildingDamageCluster2Z); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.BuildingDamageCluster2Strength); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.BuildingDamageCluster2Age); offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], e.BuildingDamageCluster2RepairFill); offset += 4;
            span[offset++] = e.BuildingDamageLayerState;
            span[offset++] = e.MountedWeaponFaultCode;
            span[offset++] = e.MountedWeaponRepairStep;
            span[offset++] = e.MountedWeaponRepairStepCount;
            span[offset++] = e.MountedWeaponMaterialUnits;

            byte flags = 0;
            if (e.IsAlive) flags |= 0x01;
            if (e.AttachToTerrain) flags |= 0x02;
            if (e.WeaponDefId > 0) flags |= 0x04;
            if (e.IsReloading) flags |= 0x08;
            if (e.AttachedVehicleEntityId > 0) flags |= 0x10;
            if (e.HasMountedWeapon) flags |= 0x20;
            span[offset++] = flags;
        }

        return buffer;
    }

    private static Quaternion NormalizeQuaternion(Quaternion q)
    {
        var lengthSq = q.LengthSquared();
        if (!float.IsFinite(lengthSq) || lengthSq < 1e-6f)
            return Quaternion.Identity;

        return Quaternion.Normalize(q);
    }

    /// <summary>
    /// Deserializes a binary snapshot buffer into entity state records.
    /// Used on the client side (Ark's SnapshotApplier).
    /// </summary>
    public static EntitySnapshot[] Deserialize(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4) return [];

        var count = BinaryPrimitives.ReadInt32LittleEndian(data);
        if (data.Length < 4 + count * BytesPerEntity) return [];

        var result = new EntitySnapshot[count];
        var offset = 4;

        for (int i = 0; i < count; i++)
        {
            var id = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]); offset += 4;
            var networkId = new Guid(data.Slice(offset, 16)); offset += 16;
            var type = (ServerEntityType)data[offset++];
            var typeId = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]); offset += 4;

            var px = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var py = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var pz = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;

            var rx = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var ry = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var rz = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var rw = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;

            var vx = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var vy = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var vz = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;

            var health = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var maxHealth = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;

            var weaponDefId = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]); offset += 4;
            var currentMag = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]); offset += 4;
            var magCapacity = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]); offset += 4;
            var reserveAmmo = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]); offset += 4;
            var maxReserve = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]); offset += 4;

            var attachedVehicleId = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]); offset += 4;
            var seatOffsetX = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var seatOffsetY = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var seatOffsetZ = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;

            var fuelPercent = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var turretYaw = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var turretPitch = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var mountedWeaponHeat = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var mountedWeaponCycleRemaining = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var mountedWeaponReloadRemaining = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var mountedWeaponFaultRemaining = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var mountedWeaponMaintenanceRemaining = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var mountedWeaponMaintenanceLevel = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var mountedWeaponOperationProgress = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var mountedWeaponSkillScalar = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var altitude = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var orbitalVelocity = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var remainingDeltaV = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;

            var seatCount = BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]); offset += 2;
            var occupiedSeatCount = BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]); offset += 2;

            var weaponCategory = data[offset++];
            var seatIndex = data[offset++];
            var seatType = data[offset++];
            var spaceFlightPhase = data[offset++];
            var buildingLevel = data[offset++];
            var buildingConstructionProgress = data[offset++];
            var buildingFrontDamage = data[offset++];
            var buildingBackDamage = data[offset++];
            var buildingRightDamage = data[offset++];
            var buildingLeftDamage = data[offset++];
            var cluster0X = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var cluster0Y = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var cluster0Z = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var cluster0Strength = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var cluster0Age = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var cluster0RepairFill = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var cluster1X = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var cluster1Y = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var cluster1Z = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var cluster1Strength = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var cluster1Age = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var cluster1RepairFill = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var cluster2X = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var cluster2Y = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var cluster2Z = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var cluster2Strength = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var cluster2Age = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var cluster2RepairFill = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]); offset += 4;
            var buildingDamageLayerState = data[offset++];
            var mountedWeaponFaultCode = data[offset++];
            var mountedWeaponRepairStep = data[offset++];
            var mountedWeaponRepairStepCount = data[offset++];
            var mountedWeaponMaterialUnits = data[offset++];
            var flags = data[offset++];

            result[i] = new EntitySnapshot(
                id, networkId, type, typeId,
                new Vector3(px, py, pz),
                new Quaternion(rx, ry, rz, rw),
                new Vector3(vx, vy, vz),
                health, maxHealth,
                weaponDefId,
                weaponCategory,
                currentMag,
                magCapacity,
                reserveAmmo,
                maxReserve,
                attachedVehicleId,
                seatIndex,
                seatType,
                (flags & 0x20) != 0,
                new Vector3(seatOffsetX, seatOffsetY, seatOffsetZ),
                seatCount,
                occupiedSeatCount,
                fuelPercent,
                turretYaw,
                turretPitch,
                mountedWeaponHeat,
                mountedWeaponCycleRemaining,
                mountedWeaponReloadRemaining,
                mountedWeaponFaultRemaining,
                mountedWeaponMaintenanceRemaining,
                mountedWeaponMaintenanceLevel,
                mountedWeaponOperationProgress,
                mountedWeaponSkillScalar,
                spaceFlightPhase,
                buildingLevel,
                buildingConstructionProgress,
                buildingFrontDamage,
                buildingBackDamage,
                buildingRightDamage,
                buildingLeftDamage,
                cluster0X,
                cluster0Y,
                cluster0Z,
                cluster0Strength,
                cluster0Age,
                cluster0RepairFill,
                cluster1X,
                cluster1Y,
                cluster1Z,
                cluster1Strength,
                cluster1Age,
                cluster1RepairFill,
                cluster2X,
                cluster2Y,
                cluster2Z,
                cluster2Strength,
                cluster2Age,
                cluster2RepairFill,
                buildingDamageLayerState,
                mountedWeaponFaultCode,
                mountedWeaponRepairStep,
                mountedWeaponRepairStepCount,
                mountedWeaponMaterialUnits,
                altitude,
                orbitalVelocity,
                remainingDeltaV,
                (flags & 0x08) != 0,
                (flags & 0x01) != 0,
                (flags & 0x02) != 0);
        }

        return result;
    }
}

/// <summary>
/// A single entity's state within a snapshot frame.
/// </summary>
public readonly record struct EntitySnapshot(
    int Id,
    Guid NetworkId,
    ServerEntityType Type,
    int TypeId,
    Vector3 Position,
    Quaternion Rotation,
    Vector3 Velocity,
    float Health,
    float MaxHealth,
    int WeaponDefId,
    byte WeaponCategory,
    int CurrentMag,
    int MagCapacity,
    int ReserveAmmo,
    int MaxReserve,
    int AttachedVehicleEntityId,
    byte SeatIndex,
    byte SeatType,
    bool HasMountedWeapon,
    Vector3 SeatOffset,
    ushort SeatCount,
    ushort OccupiedSeatCount,
    float FuelPercent,
    float TurretYaw,
    float TurretPitch,
    float MountedWeaponHeat,
    float MountedWeaponCycleRemaining,
    float MountedWeaponReloadRemaining,
    float MountedWeaponFaultRemaining,
    float MountedWeaponMaintenanceRemaining,
    float MountedWeaponMaintenanceLevel,
    float MountedWeaponOperationProgress,
    float MountedWeaponSkillScalar,
    byte SpaceFlightPhase,
    byte BuildingLevel,
    byte BuildingConstructionProgress,
    byte BuildingFrontDamage,
    byte BuildingBackDamage,
    byte BuildingRightDamage,
    byte BuildingLeftDamage,
    float BuildingDamageCluster0X,
    float BuildingDamageCluster0Y,
    float BuildingDamageCluster0Z,
    float BuildingDamageCluster0Strength,
    float BuildingDamageCluster0Age,
    float BuildingDamageCluster0RepairFill,
    float BuildingDamageCluster1X,
    float BuildingDamageCluster1Y,
    float BuildingDamageCluster1Z,
    float BuildingDamageCluster1Strength,
    float BuildingDamageCluster1Age,
    float BuildingDamageCluster1RepairFill,
    float BuildingDamageCluster2X,
    float BuildingDamageCluster2Y,
    float BuildingDamageCluster2Z,
    float BuildingDamageCluster2Strength,
    float BuildingDamageCluster2Age,
    float BuildingDamageCluster2RepairFill,
    byte BuildingDamageLayerState,
    byte MountedWeaponFaultCode,
    byte MountedWeaponRepairStep,
    byte MountedWeaponRepairStepCount,
    byte MountedWeaponMaterialUnits,
    float Altitude,
    float OrbitalVelocity,
    float RemainingDeltaV,
    bool IsReloading,
    bool IsAlive,
    bool AttachToTerrain);
