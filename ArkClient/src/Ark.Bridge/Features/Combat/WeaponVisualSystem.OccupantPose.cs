using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Shared.Data;
using Friflo.Engine.ECS;
using Godot;

namespace Ark.Bridge.Features.Combat;

public partial class WeaponVisualSystem
{
    private void UpdateAttachmentPose(WeaponAttachment att)
    {
        var entity = ResolveEntity(att.EntityId);
        if (entity.IsNull)
            return;

        byte weaponCategory = ResolveWeaponCategory(entity, att.WeaponCategory);
        if (weaponCategory != att.WeaponCategory)
            UpdateAttachmentGeometry(att, weaponCategory);

        bool isReloading = entity.TryGetComponent<RemoteCombatState>(out var remoteCombat)
            ? remoteCombat.IsReloading != 0
            : entity.TryGetComponent<WeaponState>(out var weaponState) && weaponState.IsReloading != 0;

        if (entity.TryGetComponent<VehicleSeat>(out var seat))
        {
            byte seatType = entity.TryGetComponent<RemoteVehicleOccupantState>(out var vehicleRuntime)
                ? vehicleRuntime.CurrentSeatType
                : seat.SeatType;
            bool mountedWeapon = entity.TryGetComponent<RemoteVehicleOccupantState>(out vehicleRuntime)
                && vehicleRuntime.HasMountedWeapon != 0;
            ApplySeatPose(att, entity, seat, (SeatType)seatType, mountedWeapon, isReloading);
            return;
        }

        ApplyStandingPose(att, isReloading);
    }

    private static byte ResolveWeaponCategory(Entity entity, byte fallbackCategory)
    {
        if (entity.TryGetComponent<WeaponState>(out var weaponState) && weaponState.WeaponDefId > 0)
            return weaponState.Category;

        if (entity.TryGetComponent<RemoteCombatState>(out var remoteCombatState) && remoteCombatState.HasWeapon != 0)
            return remoteCombatState.WeaponCategory;

        return fallbackCategory;
    }

    private static void ApplyStandingPose(WeaponAttachment att, bool isReloading)
    {
        att.Root.Position = att.Root.Position.Lerp(new Vector3(0.35f, 1.1f, -0.5f), 0.18f);
        att.Root.RotationDegrees = att.Root.RotationDegrees.Lerp(new Vector3(-4f, -6f, 0f), 0.18f);
        att.Root.Scale = att.Root.Scale.Lerp(Vector3.One, 0.18f);
        att.GunRestPos = isReloading ? new Vector3(0.02f, -0.05f, 0.04f) : Vector3.Zero;
    }

    private void ApplySeatPose(WeaponAttachment att, Entity occupantEntity, VehicleSeat seat, SeatType seatType, bool mountedWeapon, bool isReloading)
    {
        Vector3 targetPos;
        Vector3 targetRot;
        Vector3 targetScale;
        Vector3 gunRestPos;
        float aimYaw = 0f;
        float aimPitch = 0f;
        bool isSpacecraftSeat = false;

        if (TryGetSeatVehicleEntity(seat.VehicleEntityId, out var vehicleEntity) && !vehicleEntity.IsNull)
        {
            if (vehicleEntity.Tags.Has<Ark.Ecs.Tags.SpacecraftTag>())
                isSpacecraftSeat = true;
        }

        if (occupantEntity.TryGetComponent<TurretState>(out var occupantTurret))
        {
            aimYaw = Mathf.RadToDeg(occupantTurret.Yaw);
            aimPitch = Mathf.RadToDeg(occupantTurret.Pitch);
        }
        else if (vehicleEntity is { IsNull: false })
        {
            if (vehicleEntity.TryGetComponent<TurretState>(out var turret))
            {
                aimYaw = Mathf.RadToDeg(turret.Yaw);
                aimPitch = Mathf.RadToDeg(turret.Pitch);
            }
            else if (vehicleEntity.TryGetComponent<WorldRotation>(out var vehicleRot))
            {
                var quat = new Quaternion(vehicleRot.X, vehicleRot.Y, vehicleRot.Z, vehicleRot.W);
                aimYaw = Mathf.RadToDeg(quat.GetEuler().Y);
            }
        }

        switch (seatType)
        {
            case SeatType.Driver:
                targetPos = isSpacecraftSeat
                    ? new Vector3(0.02f, 0.86f, -0.48f)
                    : mountedWeapon ? new Vector3(0.08f, 0.98f, -0.9f) : new Vector3(0.18f, 0.94f, -0.22f);
                targetRot = isSpacecraftSeat
                    ? new Vector3(-6f - aimPitch * 0.08f, aimYaw * 0.05f, -4f)
                    : mountedWeapon ? new Vector3(-12f - aimPitch * 0.12f, aimYaw * 0.06f, 0f) : new Vector3(16f, -72f + aimYaw * 0.04f, 26f);
                targetScale = isSpacecraftSeat ? new Vector3(0.92f, 0.92f, 1.05f) : mountedWeapon ? new Vector3(1.12f, 1.12f, 1.25f) : new Vector3(0.82f, 0.82f, 0.82f);
                gunRestPos = mountedWeapon ? Vector3.Zero : new Vector3(0.05f, -0.06f, 0.10f);
                break;
            case SeatType.Gunner:
                targetPos = isSpacecraftSeat
                    ? new Vector3(0.0f, 1.02f, -0.86f)
                    : mountedWeapon ? new Vector3(0.0f, 1.12f, -1.05f) : new Vector3(0.14f, 1.0f, -0.36f);
                targetRot = isSpacecraftSeat
                    ? new Vector3(-8f - aimPitch * 0.16f, aimYaw * 0.10f, -6f)
                    : mountedWeapon ? new Vector3(-6f - aimPitch * 0.28f, aimYaw * 0.12f, 0f) : new Vector3(2f - aimPitch * 0.10f, -14f + aimYaw * 0.05f, 0f);
                targetScale = isSpacecraftSeat ? new Vector3(1.1f, 1.08f, 1.28f) : mountedWeapon ? new Vector3(1.25f, 1.25f, 1.5f) : new Vector3(0.95f, 0.95f, 1.05f);
                gunRestPos = mountedWeapon ? new Vector3(0f, 0.02f, 0.02f) : Vector3.Zero;
                break;
            default:
                targetPos = isSpacecraftSeat ? new Vector3(-0.08f, 0.82f, 0.26f) : new Vector3(-0.18f, 0.88f, 0.08f);
                targetRot = isSpacecraftSeat ? new Vector3(12f - aimPitch * 0.04f, aimYaw * 0.02f, 88f) : new Vector3(84f, 28f, 94f);
                targetScale = isSpacecraftSeat ? new Vector3(0.82f, 0.82f, 0.88f) : new Vector3(0.74f, 0.74f, 0.74f);
                gunRestPos = new Vector3(0.03f, -0.02f, 0.12f);
                break;
        }

        att.Root.Position = att.Root.Position.Lerp(targetPos, 0.22f);
        att.Root.RotationDegrees = att.Root.RotationDegrees.Lerp(targetRot, 0.22f);
        att.Root.Scale = att.Root.Scale.Lerp(targetScale, 0.22f);
        att.GunRestPos = isReloading ? gunRestPos + new Vector3(0f, -0.04f, 0.05f) : gunRestPos;

        if (occupantEntity.TryGetComponent<MountedWeaponRuntimeState>(out var mountedRuntime))
        {
            float cycleKick = mountedRuntime.FireCycleNormalized;
            att.Root.Scale = att.Root.Scale.Lerp(targetScale + Vector3.One * (mountedRuntime.Heat * 0.08f + cycleKick * 0.04f), 0.18f);
            float reloadKick = mountedRuntime.ReloadNormalized;
            float faultKick = mountedRuntime.FaultCode != 0 ? Mathf.Clamp(mountedRuntime.FaultRemaining, 0f, 1f) : 0f;
            float maintenanceKick = mountedRuntime.IsMaintaining != 0 ? Mathf.Clamp(mountedRuntime.MaintenanceRemaining * 0.18f, 0f, 1f) : 0f;
            if (mountedRuntime.IsMaintaining != 0 && att.MaintenanceTimer <= 0.01f)
                att.MaintenanceTimer = MaintenanceAnimDuration;
            att.Root.RotationDegrees = att.Root.RotationDegrees.Lerp(targetRot + new Vector3(reloadKick * 18f + faultKick * 10f, maintenanceKick * 6f, faultKick * 12f), 0.16f);
            att.GunRestPos += new Vector3(maintenanceKick * 0.04f, mountedRuntime.Heat * 0.02f - reloadKick * 0.06f, cycleKick * 0.08f + reloadKick * 0.12f);
        }
    }

    private bool TryGetSeatVehicleEntity(int snapshotVehicleId, out Entity entity)
    {
        entity = default;
        if (_store is null)
            return false;

        entity = _store.GetEntityById(snapshotVehicleId);
        if (!entity.IsNull)
            return true;

        if (_remoteWorldEcsCache != null && _remoteWorldEcsCache.TryGetEcsEntityId(snapshotVehicleId, out var ecsVehicleId))
        {
            entity = _store.GetEntityById(ecsVehicleId);
            return !entity.IsNull;
        }

        return false;
    }

    private static void UpdateAttachmentGeometry(WeaponAttachment att, byte weaponCategory)
    {
        var (gunSize, barrelLen) = GetGunDimensions(weaponCategory);
        att.WeaponCategory = weaponCategory;
        att.Gun.Mesh = new BoxMesh { Size = gunSize, Material = _gunMetalMat };

        if (att.Barrel.Mesh is CylinderMesh barrelMesh)
        {
            barrelMesh.TopRadius = 0.015f + weaponCategory * 0.002f;
            barrelMesh.BottomRadius = 0.02f + weaponCategory * 0.002f;
            barrelMesh.Height = barrelLen;
            barrelMesh.Material = _gunMetalMat;
            att.Barrel.Position = new Vector3(0, 0, -(gunSize.Z * 0.5f + barrelLen * 0.5f));
        }

        att.Muzzle.Position = new Vector3(0, 0, -(gunSize.Z * 0.5f + barrelLen));
    }
}
