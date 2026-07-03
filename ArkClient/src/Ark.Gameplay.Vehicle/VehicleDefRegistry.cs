using System.Collections.Generic;
using Ark.Shared.Data;

namespace Ark.Gameplay.Vehicle;

/// <summary>
/// 载具定义注册表。
/// </summary>
public sealed class VehicleDefRegistry
{
    private readonly Dictionary<int, VehicleDef> _defs = [];

    public void Register(VehicleDef def) => _defs[def.Id] = def;
    public VehicleDef? Get(int id) => _defs.TryGetValue(id, out var d) ? d : null;
    public IEnumerable<VehicleDef> GetAll() => _defs.Values;

    /// <summary>
    /// 注册默认载具定义。
    /// </summary>
    public void RegisterDefaults()
    {
        Register(new VehicleDef(
            Id: 1, Name: "越野车",
            Type: VehicleType.Car, MaxHealth: 500f, MaxSpeed: 25f,
            Acceleration: 8f, TurnSpeed: 3f, MaxFuel: 100f, FuelConsumption: 0.05f,
            Seats: [
                new(0, SeatType.Driver, new(0, 0.5f, 0.5f), false, 0),
                new(1, SeatType.Passenger, new(0.8f, 0.5f, 0.5f), false, 0),
                new(2, SeatType.Passenger, new(-0.8f, 0.5f, -0.5f), false, 0),
                new(3, SeatType.Passenger, new(0.8f, 0.5f, -0.5f), false, 0),
            ],
            TurretWeaponDefIds: [],
            ArmorPhysical: 0.3f, ArmorEnergy: 0.1f
        ));

        Register(new VehicleDef(
            Id: 2, Name: "主战坦克",
            Type: VehicleType.Tank, MaxHealth: 3000f, MaxSpeed: 12f,
            Acceleration: 3f, TurnSpeed: 1.5f, MaxFuel: 300f, FuelConsumption: 0.2f,
            Seats: [
                new(0, SeatType.Driver, new(0, 1f, 1f), false, 0),
                new(1, SeatType.Gunner, new(0, 2f, 0), true, 60),
                new(2, SeatType.Gunner, new(0, 2.5f, -0.5f), true, 61),
            ],
            TurretWeaponDefIds: [60],
            ArmorPhysical: 0.8f, ArmorEnergy: 0.5f
        ));

        Register(new VehicleDef(
            Id: 3, Name: "防空炮",
            Type: VehicleType.AntiAir, MaxHealth: 800f, MaxSpeed: 0f,
            Acceleration: 0f, TurnSpeed: 0f, MaxFuel: float.MaxValue, FuelConsumption: 0f,
            Seats: [
                new(0, SeatType.Gunner, new(0, 1.5f, 0), true, 61),
            ],
            TurretWeaponDefIds: [61],
            ArmorPhysical: 0.4f, ArmorEnergy: 0.2f
        ));

        Register(new VehicleDef(
            Id: 4, Name: "战斗机",
            Type: VehicleType.Plane, MaxHealth: 1000f, MaxSpeed: 80f,
            Acceleration: 15f, TurnSpeed: 2f, MaxFuel: 200f, FuelConsumption: 0.3f,
            Seats: [
                new(0, SeatType.Driver, new(0, 0, 1f), true, 61),
            ],
            TurretWeaponDefIds: [61],
            ArmorPhysical: 0.2f, ArmorEnergy: 0.1f
        ));

        Register(new VehicleDef(
            Id: 5, Name: "巡逻艇",
            Type: VehicleType.Boat, MaxHealth: 1500f, MaxSpeed: 15f,
            Acceleration: 4f, TurnSpeed: 1.8f, MaxFuel: 250f, FuelConsumption: 0.15f,
            Seats: [
                new(0, SeatType.Driver, new(0, 1.5f, 2f), false, 0),
                new(1, SeatType.Gunner, new(0, 2f, 0), true, 61),
                new(2, SeatType.Passenger, new(-1f, 1f, -1f), false, 0),
                new(3, SeatType.Passenger, new(1f, 1f, -1f), false, 0),
            ],
            TurretWeaponDefIds: [61],
            ArmorPhysical: 0.4f, ArmorEnergy: 0.3f
        ));

        Register(new VehicleDef(
            Id: 6, Name: "运载火箭",
            Type: VehicleType.Rocket, MaxHealth: 2000f, MaxSpeed: 100f,
            Acceleration: 20f, TurnSpeed: 0.5f, MaxFuel: 500f, FuelConsumption: 1f,
            Seats: [
                new(0, SeatType.Driver, new(0, 2f, 0), false, 0),
                new(1, SeatType.Passenger, new(0.5f, 2f, 0), false, 0),
            ],
            TurretWeaponDefIds: [],
            ArmorPhysical: 0.5f, ArmorEnergy: 0.3f
        ));
    }
}
