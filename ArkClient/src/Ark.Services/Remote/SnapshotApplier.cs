using System;
using System.Buffers.Binary;
using System.Numerics;

namespace Ark.Services.Remote;

/// <summary>
/// 快照解码器 — 解码服务端 WorldTickService 推送的 0x10 快照包。
/// 帧格式：[1:packetId=0x10][8:tick][4:serverTime][4:entityCount][N * 62字节实体状态]
/// 实体格式：
/// [4:id][16:networkId][1:type][4:typeId][12:posXYZ][16:rotXYZW][12:velXYZ][4:health][4:maxHealth]
/// [4:weaponDefId][4:currentMag][4:magCapacity][4:reserveAmmo][4:maxReserve]
/// [4:attachedVehicleId][12:seatOffsetXYZ][4:fuelPercent][4:turretYaw][4:turretPitch][4:mountedWeaponHeat][4:mountedWeaponCycle][4:mountedWeaponReload][4:mountedWeaponFault][4:mountedWeaponMaintenance][4:mountedWeaponMaintenanceLevel][4:mountedWeaponOperationProgress][4:mountedWeaponSkillScalar][4:altitude][4:orbitalVelocity][4:remainingDeltaV]
/// [2:seatCount][2:occupiedSeatCount][1:weaponCategory][1:seatIndex][1:seatType][1:spaceFlightPhase][1:buildingLevel][1:buildingConstructionProgress][1:frontDamage][1:backDamage][1:rightDamage][1:leftDamage]
/// [12:cluster0XYZ][4:cluster0Strength][4:cluster0Age][4:cluster0RepairFill][12:cluster1XYZ][4:cluster1Strength][4:cluster1Age][4:cluster1RepairFill]
/// [12:cluster2XYZ][4:cluster2Strength][4:cluster2Age][4:cluster2RepairFill][1:buildingDamageLayerState][1:mountedWeaponFaultCode][1:mountedWeaponRepairStep][1:mountedWeaponRepairStepCount][1:mountedWeaponMaterialUnits][1:flags]
/// </summary>
public sealed class SnapshotApplier
{
    public const byte SnapshotPacketId = 0x10;
    private const int BytesPerEntity = 258;

    private readonly RemoteGameWorld _world;

    public ulong LastTick { get; private set; }
    public float LastServerTime { get; private set; }

    public SnapshotApplier(RemoteGameWorld world)
    {
        _world = world;
    }

    private ulong _logCounter;

    /// <summary>
    /// 处理 TCP/SignalR 原始消息 — 如果是快照包则解码并应用。
    /// 返回 true 表示已处理。
    /// </summary>
    public bool TryApply(ReadOnlyMemory<byte> data)
    {
        var span = data.Span;
        if (span.Length < 1 || span[0] != SnapshotPacketId) return false;

        // Minimum: 1 (packetId) + 8 (tick) + 4 (serverTime) + 4 (entityCount) = 17
        if (span.Length < 17)
        {
            ServiceLog.Error($"[SnapshotApplier] ❌ Packet too short: {span.Length} bytes");
            return false;
        }

        var tick = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(1));
        var serverTime = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(9));
        var entityCount = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(13));

        if (span.Length < 17 + entityCount * BytesPerEntity)
        {
            ServiceLog.Error($"[SnapshotApplier] ❌ Truncated: need {17 + entityCount * BytesPerEntity}, got {span.Length}");
            return false;
        }

        var entities = new SnapshotEntity[entityCount];
        var offset = 17;

        for (int i = 0; i < entityCount; i++)
        {
            var id = BinaryPrimitives.ReadInt32LittleEndian(span[offset..]); offset += 4;
            var networkId = new Guid(span.Slice(offset, 16)); offset += 16;
            var type = span[offset++];
            var typeId = BinaryPrimitives.ReadInt32LittleEndian(span[offset..]); offset += 4;

            var px = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var py = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var pz = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;

            var rx = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var ry = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var rz = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var rw = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;

            var vx = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var vy = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var vz = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;

            var health = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var maxHealth = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;

            var weaponDefId = BinaryPrimitives.ReadInt32LittleEndian(span[offset..]); offset += 4;
            var currentMag = BinaryPrimitives.ReadInt32LittleEndian(span[offset..]); offset += 4;
            var magCapacity = BinaryPrimitives.ReadInt32LittleEndian(span[offset..]); offset += 4;
            var reserveAmmo = BinaryPrimitives.ReadInt32LittleEndian(span[offset..]); offset += 4;
            var maxReserve = BinaryPrimitives.ReadInt32LittleEndian(span[offset..]); offset += 4;

            var attachedVehicleId = BinaryPrimitives.ReadInt32LittleEndian(span[offset..]); offset += 4;
            var seatOffsetX = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var seatOffsetY = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var seatOffsetZ = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;

            var fuelPercent = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var turretYaw = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var turretPitch = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var mountedWeaponHeat = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var mountedWeaponCycleRemaining = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var mountedWeaponReloadRemaining = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var mountedWeaponFaultRemaining = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var mountedWeaponMaintenanceRemaining = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var mountedWeaponMaintenanceLevel = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var mountedWeaponOperationProgress = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var mountedWeaponSkillScalar = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var altitude = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var orbitalVelocity = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var remainingDeltaV = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;

            var seatCount = BinaryPrimitives.ReadUInt16LittleEndian(span[offset..]); offset += 2;
            var occupiedSeatCount = BinaryPrimitives.ReadUInt16LittleEndian(span[offset..]); offset += 2;
            var weaponCategory = span[offset++];
            var seatIndex = span[offset++];
            var seatType = span[offset++];
            var spaceFlightPhase = span[offset++];
            var buildingLevel = span[offset++];
            var buildingConstructionProgress = span[offset++];
            var buildingFrontDamage = span[offset++];
            var buildingBackDamage = span[offset++];
            var buildingRightDamage = span[offset++];
            var buildingLeftDamage = span[offset++];
            var cluster0X = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var cluster0Y = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var cluster0Z = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var cluster0Strength = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var cluster0Age = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var cluster0RepairFill = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var cluster1X = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var cluster1Y = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var cluster1Z = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var cluster1Strength = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var cluster1Age = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var cluster1RepairFill = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var cluster2X = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var cluster2Y = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var cluster2Z = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var cluster2Strength = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var cluster2Age = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var cluster2RepairFill = BinaryPrimitives.ReadSingleLittleEndian(span[offset..]); offset += 4;
            var buildingDamageLayerState = span[offset++];
            var mountedWeaponFaultCode = span[offset++];
            var mountedWeaponRepairStep = span[offset++];
            var mountedWeaponRepairStepCount = span[offset++];
            var mountedWeaponMaterialUnits = span[offset++];
            var flags = span[offset++];

            entities[i] = new SnapshotEntity(
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

        // Periodic diagnostic (every ~5 seconds at 5Hz = every 25 snapshots)
        _logCounter++;
        if (_logCounter % 25 == 1)
        {
            ServiceLog.Info($"[SnapshotApplier] ✅ tick={tick}, entities={entityCount}, bytes={data.Length}, localPlayer={_world.LocalPlayerGuid}");
            for (int i = 0; i < entityCount && i < 5; i++)
            {
                var e = entities[i];
                ServiceLog.Info($"  Entity[{e.Id}] netId={e.NetworkId}, type={e.Type}, pos=({e.Position.X:F1},{e.Position.Y:F1},{e.Position.Z:F1})");
            }
        }

        LastTick = tick;
        LastServerTime = serverTime;
        _world.ApplySnapshot(tick, serverTime, entities);
        return true;
    }
}
