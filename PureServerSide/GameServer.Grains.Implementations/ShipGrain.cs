using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;

namespace GameServer.Grains.Implementations;

public sealed class ShipGrain(
    [PersistentState("ship", "GameStore")] IPersistentState<ShipGrainState> state,
    ILogger<ShipGrain> logger) : Grain, IShipGrain
{
    public async Task InitializeAsync(Guid ownerId, string shipType, double hull, double shield, double armor)
    {
        state.State.OwnerId = ownerId;
        state.State.ShipType = shipType;
        state.State.HullPoints = hull;
        state.State.MaxHull = hull;
        state.State.ShieldPoints = shield;
        state.State.MaxShield = shield;
        state.State.ArmorPoints = armor;
        state.State.MaxArmor = armor;
        await state.WriteStateAsync();
    }

    public async Task<double> ApplyDamageAsync(double damage)
    {
        var remaining = damage;

        if (state.State.ShieldPoints > 0)
        {
            var absorbed = Math.Min(state.State.ShieldPoints, remaining);
            state.State.ShieldPoints -= absorbed;
            remaining -= absorbed;
        }

        if (remaining > 0 && state.State.ArmorPoints > 0)
        {
            var absorbed = Math.Min(state.State.ArmorPoints, remaining);
            state.State.ArmorPoints -= absorbed;
            remaining -= absorbed;
        }

        if (remaining > 0)
        {
            state.State.HullPoints = Math.Max(0, state.State.HullPoints - remaining);
        }

        state.State.IsDestroyed = state.State.HullPoints <= 0;
        await state.WriteStateAsync();

        logger.LogInformation("Ship {Id} took {Damage} damage. Hull: {Hull}",
            this.GetPrimaryKey(), damage, state.State.HullPoints);

        return damage;
    }

    public async Task RepairShieldAsync(double amount)
    {
        state.State.ShieldPoints = Math.Min(state.State.MaxShield, state.State.ShieldPoints + amount);
        await state.WriteStateAsync();
    }

    public Task<ShipGrainState> GetStateAsync() => Task.FromResult(state.State);

    public Task<bool> IsDestroyedAsync() => Task.FromResult(state.State.IsDestroyed);

    public async Task SetLocationAsync(string solarSystemId)
    {
        state.State.CurrentSolarSystemId = solarSystemId;
        await state.WriteStateAsync();
    }
}
