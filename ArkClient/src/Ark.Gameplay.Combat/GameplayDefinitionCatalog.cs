using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Gameplay.Vehicle;

namespace Ark.Gameplay.Combat;

public sealed class GameplayDefinitionCatalog
{
    public WeaponDefRegistry WeaponDefs { get; } = new();
    public ProjectileDefRegistry ProjectileDefs { get; } = new();
    public VehicleDefRegistry VehicleDefs { get; } = new();

    public GameplayDefinitionCatalog()
    {
        WeaponDefs.RegisterDefaults();
        ProjectileDefs.RegisterDefaults();
        VehicleDefs.RegisterDefaults();
    }
}

public static class PresentationCombatState
{
    public static void SeedDefaultLoadout(Entity entity, GameplayDefinitionCatalog definitions, int weaponDefId = 20)
    {
        var weaponDef = definitions.WeaponDefs.Get(weaponDefId);
        if (weaponDef is null)
            return;

        entity.AddComponent(new WeaponState
        {
            WeaponDefId = weaponDef.Value.Id,
            Category = (byte)weaponDef.Value.Category,
            SlotIndex = 0,
            IsReloading = 0,
        });

        entity.AddComponent(new AmmoState
        {
            CurrentMag = weaponDef.Value.MagCapacity,
            MagCapacity = weaponDef.Value.MagCapacity,
            ReserveAmmo = weaponDef.Value.MaxReserve,
            MaxReserve = weaponDef.Value.MaxReserve,
        });
    }
}
