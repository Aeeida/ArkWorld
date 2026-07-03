using System.Numerics;
using Game.Shared.Core.DTOs;
using GameServer.Infrastructure.Persistence;
using GameLayer.Building;
using GameLayer.Combat;
using GameLayer.Core;
using GameLayer.Inventory;
using GameLayer.Vehicle;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameServer.Networking.SignalR;

public sealed partial class GameHub
{
    // ══════════════════════════════════════════════════════════════════
    // 武器开火/换弹（服务端权威）
    // ══════════════════════════════════════════════════════════════════

    public async Task<FireWeaponResultDto> FireWeaponAsync(FireWeaponCommandDto cmd)
    {
        var world = serviceProvider.GetRequiredService<ServerWorldState>();
        var weapons = serviceProvider.GetRequiredService<WeaponRegistry>();
        var combat = serviceProvider.GetRequiredService<CombatSessionManager>();
        var buildings = serviceProvider.GetRequiredService<BuildingManager>();
        var vehicles = serviceProvider.GetRequiredService<GameLayer.Vehicle.VehicleManager>();

        var shooterEntity = world.Entities.GetByNetworkId(cmd.PlayerId);
        if (shooterEntity is null)
            return new FireWeaponResultDto(false, 0, null, 0, 0, 0, 0, false, false, 0, "Player entity not found.");

        var weaponDefId = cmd.WeaponDefId;
        var seatedVehicle = vehicles.GetPlayerVehicle(cmd.PlayerId);
        VehicleSeatRuntime? mountedSeatRuntime = null;
        if (seatedVehicle is not null)
        {
            var vehicleState = vehicles.GetVehicle(seatedVehicle.Value.VehicleId);
            if (vehicleState is null)
                return new FireWeaponResultDto(false, shooterEntity.Id, null, 0, 0, 0, 0, false, false, 0, "Vehicle state not found.");

            var seatIndex = seatedVehicle.Value.SeatIndex;
            if (seatIndex < 0 || seatIndex >= vehicleState.Definition.Seats.Length)
                return new FireWeaponResultDto(false, shooterEntity.Id, null, 0, 0, 0, 0, false, false, 0, "Invalid vehicle seat.");

            var seatDef = vehicleState.Definition.Seats[seatIndex];
            if (!seatDef.HasWeapon || seatDef.WeaponDefId <= 0)
                return new FireWeaponResultDto(false, shooterEntity.Id, null, 0, 0, 0, 0, false, false, 0, "Current vehicle seat has no weapon.");

            if (weaponDefId > 0 && weaponDefId != seatDef.WeaponDefId)
                return new FireWeaponResultDto(false, shooterEntity.Id, null, 0, 0, 0, 0, false, false, 0, "Weapon not allowed for current vehicle seat.");

            weaponDefId = seatDef.WeaponDefId;
            mountedSeatRuntime = vehicles.GetSeatRuntime(seatedVehicle.Value.VehicleId, seatIndex);
        }

        var weaponDef = weapons.Get(weaponDefId);
        if (weaponDef is null)
            return new FireWeaponResultDto(false, shooterEntity.Id, null, 0, 0, 0, 0, false, false, 0, "Unknown weapon.");

        if (mountedSeatRuntime is { } mountedRuntime)
        {
            if (mountedRuntime.CurrentMag <= 0)
                return new FireWeaponResultDto(false, shooterEntity.Id, null, 0, 0, 0, 0, false, false, mountedRuntime.CurrentMag, "Mounted weapon is empty.");
        }
        else
        {
            if (shooterEntity.WeaponDefId <= 0)
                weapons.TryApplyLoadout(shooterEntity, weaponDefId);

            if (shooterEntity.CurrentMag <= 0)
                return new FireWeaponResultDto(false, shooterEntity.Id, null, 0, 0, 0, 0, false, false, shooterEntity.CurrentMag, "Weapon is empty.");
        }

        if (mountedSeatRuntime is not null)
        {
            vehicles.RefreshMountedWeaponState(seatedVehicle!.Value.VehicleId, seatedVehicle.Value.SeatIndex);
            if (mountedSeatRuntime.CurrentMag <= 0)
                return new FireWeaponResultDto(false, shooterEntity.Id, null, 0, 0, 0, 0, false, false, mountedSeatRuntime.CurrentMag, "Mounted weapon is empty.");
            if (mountedSeatRuntime.ReloadRemaining > 0f)
                return new FireWeaponResultDto(false, shooterEntity.Id, null, 0, 0, 0, 0, false, false, mountedSeatRuntime.CurrentMag, "Mounted weapon is reloading.");
            if (mountedSeatRuntime.FaultRemaining > 0f)
                return new FireWeaponResultDto(false, shooterEntity.Id, null, 0, 0, 0, 0, false, false, mountedSeatRuntime.CurrentMag,
                    mountedSeatRuntime.FaultCode switch
                    {
                        2 => "Mounted weapon overheated.",
                        3 => "Mounted weapon feed assembly stalled.",
                        4 => "Mounted weapon alignment drift detected.",
                        _ => "Mounted weapon jammed."
                    });
            if (mountedSeatRuntime.FireCycleRemaining > 0f)
                return new FireWeaponResultDto(false, shooterEntity.Id, null, 0, 0, 0, 0, false, false, mountedSeatRuntime.CurrentMag, "Mounted weapon cycle active.");
            if (mountedSeatRuntime.Heat >= 0.995f)
                return new FireWeaponResultDto(false, shooterEntity.Id, null, 0, 0, 0, 0, false, false, mountedSeatRuntime.CurrentMag, "Mounted weapon overheated.");
        }
        else if (!combat.CanFire(cmd.PlayerId, weaponDef.Value.FireInterval))
        {
            return new FireWeaponResultDto(false, shooterEntity.Id, null, 0, 0, 0, 0, false, false, 0, "Weapon on cooldown.");
        }

        // Raycast: find nearest entity in firing direction
        var origin = new Vector3((float)cmd.OriginX, (float)cmd.OriginY, (float)cmd.OriginZ);
        var dir = Vector3.Normalize(new Vector3((float)cmd.DirX, (float)cmd.DirY, (float)cmd.DirZ));
        if (seatedVehicle is not null)
            vehicles.TryUpdateMountedAim(cmd.PlayerId, seatedVehicle.Value.VehicleId, dir);
        var hitEntity = RaycastEntities(world, shooterEntity.WorldId, origin, dir, weaponDef.Value.Range, shooterEntity.Id);

        float damage = 0;
        bool isCrit = false, isKill = false;
        double hitX = origin.X + dir.X * weaponDef.Value.Range;
        double hitY = origin.Y + dir.Y * weaponDef.Value.Range;
        double hitZ = origin.Z + dir.Z * weaponDef.Value.Range;
        int? hitEntityId = null;

        if (hitEntity is not null)
        {
            var dist = Vector3.Distance(origin, hitEntity.Position);
            var dmg = DamageCalculator.Calculate(shooterEntity, hitEntity, weaponDef.Value, HitZone.Body, dist);
            isKill = DamageCalculator.ApplyDamage(hitEntity, dmg.FinalDamage);
            damage = dmg.FinalDamage;
            isCrit = dmg.IsCrit;
            hitX = hitEntity.Position.X;
            hitY = hitEntity.Position.Y;
            hitZ = hitEntity.Position.Z;
            hitEntityId = hitEntity.Id;

            if (hitEntity.Type == ServerEntityType.Building)
                buildings.RecordDamage(hitEntity.Id, new Vector3((float)hitX, (float)hitY, (float)hitZ), dmg.FinalDamage, hitEntity.Rotation);
        }

        int ammoRemaining;
        if (mountedSeatRuntime is { } mountedRuntimeAfterShot)
        {
            if (!vehicles.TryConsumeMountedShot(seatedVehicle!.Value.VehicleId, seatedVehicle.Value.SeatIndex, weaponDef.Value, out mountedSeatRuntime)
                || mountedSeatRuntime is null)
                return new FireWeaponResultDto(false, shooterEntity.Id, null, 0, 0, 0, 0, false, false, mountedRuntimeAfterShot.CurrentMag, "Mounted weapon cycle active.");

            ammoRemaining = mountedSeatRuntime.CurrentMag;
        }
        else
        {
            combat.RecordAttack(cmd.PlayerId);
            shooterEntity.CurrentMag = Math.Max(0, shooterEntity.CurrentMag - 1);
            shooterEntity.PersonalCurrentMag = shooterEntity.CurrentMag;
            ammoRemaining = shooterEntity.CurrentMag;
        }

        var result = new FireWeaponResultDto(true, shooterEntity.Id, hitEntityId,
            hitX, hitY, hitZ, damage, isCrit, isKill, ammoRemaining, null);

        // Broadcast projectile event to zone
        var session = await sessionManager.GetSessionAsync(Context.ConnectionId);
        if (session?.CurrentWorldId is not null)
        {
            var travelTime = hitEntity is not null
                ? Vector3.Distance(origin, hitEntity.Position) / 300f
                : weaponDef.Value.Range / 300f;

            var projectile = new ProjectileEventDto(
                shooterEntity.Id, weaponDefId,
                cmd.OriginX, cmd.OriginY, cmd.OriginZ,
                cmd.DirX, cmd.DirY, cmd.DirZ,
                hitX, hitY, hitZ, travelTime);

            await Clients.Group(session.CurrentWorldId).SendAsync("OnProjectileBroadcast", projectile);
            await Clients.Group(session.CurrentWorldId).SendAsync("OnWeaponFired", result);
        }

        logger.LogInformation("Weapon fired: Shooter={Shooter}, Hit={Hit}, Damage={Damage}, Kill={Kill}",
            shooterEntity.Id, hitEntityId, damage, isKill);
        return result;
    }

    public Task<ReloadWeaponResultDto> ReloadWeaponAsync(ReloadWeaponCommandDto cmd)
    {
        var world = serviceProvider.GetRequiredService<ServerWorldState>();
        var vehicles = serviceProvider.GetRequiredService<GameLayer.Vehicle.VehicleManager>();
        var weapons = serviceProvider.GetRequiredService<WeaponRegistry>();
        var weaponDef = weapons.Get(cmd.WeaponDefId);
        if (weaponDef is null)
            return Task.FromResult(new ReloadWeaponResultDto(false, 0, 0, "Unknown weapon."));

        var shooterEntity = world.Entities.GetByNetworkId(cmd.PlayerId);
        if (shooterEntity is null)
            return Task.FromResult(new ReloadWeaponResultDto(false, 0, 0, "Player entity not found."));

        var seatedVehicle = vehicles.GetPlayerVehicle(cmd.PlayerId);
        if (seatedVehicle is not null)
        {
            var seatRuntime = vehicles.GetSeatRuntime(seatedVehicle.Value.VehicleId, seatedVehicle.Value.SeatIndex);
            if (seatRuntime is null)
                return Task.FromResult(new ReloadWeaponResultDto(false, 0, 0, "Mounted weapon state not found."));

            if (!vehicles.TryStartMountedReload(seatedVehicle.Value.VehicleId, seatedVehicle.Value.SeatIndex, weaponDef.Value, out seatRuntime)
                || seatRuntime is null)
                return Task.FromResult(new ReloadWeaponResultDto(false, seatRuntime?.CurrentMag ?? 0, seatRuntime?.ReserveAmmo ?? 0, "Mounted weapon cannot reload."));

            return Task.FromResult(new ReloadWeaponResultDto(true, seatRuntime.CurrentMag, seatRuntime.ReserveAmmo, null));
        }

        weapons.TryApplyLoadout(shooterEntity, weaponDef.Value.Id, weaponDef.Value.MagCapacity, weaponDef.Value.MaxReserve, setPersonal: true);
        return Task.FromResult(new ReloadWeaponResultDto(true, shooterEntity.CurrentMag, shooterEntity.ReserveAmmo, null));
    }

    public Task<SeatWeaponInteractResultDto> SeatWeaponInteractAsync(SeatWeaponInteractCommandDto cmd)
    {
        var vehicles = serviceProvider.GetRequiredService<GameLayer.Vehicle.VehicleManager>();
        var inventory = serviceProvider.GetRequiredService<InventoryManager>();
        var seatedVehicle = vehicles.GetPlayerVehicle(cmd.PlayerId);
        if (seatedVehicle is null)
            return Task.FromResult(new SeatWeaponInteractResultDto(false, 0f, 0f, 0f, 0, 0, 0, 0, 1f, "Player is not in a seat weapon vehicle."));

        float skillScalar = ResolveMountedMaintenanceSkillScalar(cmd.PlayerId);
        var seatState = vehicles.GetSeatRuntime(seatedVehicle.Value.VehicleId, seatedVehicle.Value.SeatIndex);
        if (seatState is null)
            return Task.FromResult(new SeatWeaponInteractResultDto(false, 0f, 0f, 0f, 0, 0, 0, 0, skillScalar, "Seat weapon state not found."));

        var (materialItemId, materialUnits) = ResolveSeatWeaponMaterialCost(cmd.ActionKind, seatState.FaultCode, skillScalar);
        if (materialUnits > 0 && !inventory.TryConsumeItem(cmd.PlayerId, materialItemId, materialUnits))
            return Task.FromResult(new SeatWeaponInteractResultDto(false, seatState.FaultRemaining, seatState.MaintenanceRemaining, seatState.MaintenanceLevel, seatState.FaultCode, seatState.RepairStep, seatState.RepairStepCount, 0, skillScalar, "Missing maintenance materials."));

        VehicleSeatRuntime? seatRuntime = null;
        bool success = cmd.ActionKind switch
        {
            0 => vehicles.TryClearMountedFault(seatedVehicle.Value.VehicleId, seatedVehicle.Value.SeatIndex, skillScalar, materialUnits, out seatRuntime),
            1 => vehicles.TryMaintainMountedWeapon(seatedVehicle.Value.VehicleId, seatedVehicle.Value.SeatIndex, skillScalar, materialUnits, out seatRuntime),
            _ => false,
        };

        if (!success || seatRuntime is null)
            return Task.FromResult(new SeatWeaponInteractResultDto(false, 0f, 0f, 0f, 0, 0, 0, (byte)materialUnits, skillScalar, "Seat weapon interaction rejected."));

        return Task.FromResult(new SeatWeaponInteractResultDto(
            true,
            seatRuntime.FaultRemaining,
            seatRuntime.MaintenanceRemaining,
            seatRuntime.MaintenanceLevel,
            seatRuntime.FaultCode,
            seatRuntime.RepairStep,
            seatRuntime.RepairStepCount,
            seatRuntime.MaterialUnits,
            seatRuntime.SkillScalar,
            null));
    }

    private (int ItemId, int Units) ResolveSeatWeaponMaterialCost(byte actionKind, byte faultCode, float skillScalar)
    {
        int baseUnits = actionKind == 1 ? 2 : faultCode switch
        {
            3 => 2,
            4 => 2,
            2 => 1,
            _ => 1,
        };
        int adjustedUnits = Math.Max(1, (int)MathF.Ceiling(baseUnits / MathF.Max(1f, skillScalar * 0.92f)));
        int itemId = actionKind == 1
            ? 9
            : faultCode switch
            {
                3 => 10,
                4 => 11,
                _ => 9,
            };
        return (itemId, adjustedUnits);
    }

    private float ResolveMountedMaintenanceSkillScalar(System.Guid playerId)
    {
        var db = serviceProvider.GetService<GameDbContext>();
        if (db is null)
            return 1f;

        var player = db.Players.Find(playerId);
        if (player is null)
            return 1f;

        return Math.Clamp(1f + (player.Level - 1) * 0.03f, 1f, 1.45f);
    }

    /// <summary>
    /// Simple server-side raycast — finds the nearest entity along a ray.
    /// </summary>
    private static ServerEntity? RaycastEntities(ServerWorldState world, string worldId, Vector3 origin, Vector3 dir, float maxDist, int excludeId)
    {
        ServerEntity? closest = null;
        float closestDist = maxDist;

        foreach (var entity in world.Entities.GetInWorld(worldId))
        {
            if (entity.Id == excludeId || !entity.IsAlive) continue;

            // Sphere-ray intersection (approximate: treat entity as 1m radius sphere)
            var toEntity = entity.Position - origin;
            var projection = Vector3.Dot(toEntity, dir);
            if (projection < 0 || projection > maxDist) continue;

            var closestPoint = origin + dir * projection;
            var distToRay = Vector3.Distance(closestPoint, entity.Position);

            var hitRadius = entity.Type switch
            {
                ServerEntityType.Building => 2f,
                ServerEntityType.Vehicle or ServerEntityType.Spacecraft => 3f,
                _ => 1f
            };

            if (distToRay < hitRadius && projection < closestDist)
            {
                closest = entity;
                closestDist = projection;
            }
        }

        return closest;
    }
}
