using Game.Shared.Events;
using GameServer.Grains.Interfaces;
using GameServer.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;

namespace GameServer.Grains.Implementations;

public sealed class PlayerGrain(
    [PersistentState("player", "GameStore")] IPersistentState<PlayerGrainState> state,
    IEventBus eventBus,
    ILogger<PlayerGrain> logger) : Grain, IPlayerGrain
{
    public Task<string> GetNameAsync() => Task.FromResult(state.State.Name);

    public async Task SetNameAsync(string name)
    {
        state.State.Name = name;
        await state.WriteStateAsync();
        logger.LogInformation("Player {Id} name set to {Name}", this.GetPrimaryKey(), name);
    }

    public Task<int> GetLevelAsync() => Task.FromResult(state.State.Level);

    public async Task<bool> GainExperienceAsync(long amount)
    {
        state.State.Experience += amount;
        var leveledUp = false;
        var oldLevel = state.State.Level;

        while (state.State.Experience >= state.State.Level * 1000L)
        {
            state.State.Experience -= state.State.Level * 1000L;
            state.State.Level++;
            state.State.MaxHealth = 100 + (state.State.Level - 1) * 20;
            state.State.Health = state.State.MaxHealth;
            leveledUp = true;
        }

        await state.WriteStateAsync();

        if (leveledUp)
        {
            await eventBus.PublishAsync(new PlayerLevelUpEvent(
                Guid.NewGuid(), DateTime.UtcNow,
                this.GetPrimaryKey(), oldLevel, state.State.Level));
        }

        return leveledUp;
    }

    public Task<double> GetHealthAsync() => Task.FromResult(state.State.Health);

    public async Task TakeDamageAsync(double damage)
    {
        var wasDead = state.State.Health <= 0;
        state.State.Health = Math.Max(0, state.State.Health - damage);
        await state.WriteStateAsync();

        if (!wasDead && state.State.Health <= 0)
        {
            await eventBus.PublishAsync(new PlayerDiedEvent(
                Guid.NewGuid(), DateTime.UtcNow,
                this.GetPrimaryKey(), null, state.State.CurrentWorldId ?? "unknown"));
        }
    }

    public async Task HealAsync(double amount)
    {
        state.State.Health = Math.Min(state.State.MaxHealth, state.State.Health + amount);
        await state.WriteStateAsync();
    }

    public async Task SetWorldAsync(string? worldId)
    {
        var oldWorld = state.State.CurrentWorldId;
        state.State.CurrentWorldId = worldId;
        await state.WriteStateAsync();

        if (worldId is not null)
        {
            await eventBus.PublishAsync(new PlayerJoinedWorldEvent(
                Guid.NewGuid(), DateTime.UtcNow, this.GetPrimaryKey(), worldId));
        }
        else if (oldWorld is not null)
        {
            await eventBus.PublishAsync(new PlayerLeftWorldEvent(
                Guid.NewGuid(), DateTime.UtcNow, this.GetPrimaryKey(), oldWorld));
        }
    }

    public Task<string?> GetWorldAsync() => Task.FromResult(state.State.CurrentWorldId);

    public async Task SetPositionAsync(double x, double y, double z, float rotation)
    {
        state.State.PositionX = x;
        state.State.PositionY = y;
        state.State.PositionZ = z;
        state.State.Rotation = rotation;
        await state.WriteStateAsync();
    }

    public async Task SetZoneAsync(string? zoneId)
    {
        state.State.CurrentZoneId = zoneId;
        await state.WriteStateAsync();
    }

    public async Task SetEquippedWeaponAsync(int weaponDefId)
    {
        state.State.EquippedWeaponDefId = weaponDefId;
        await state.WriteStateAsync();
    }

    public Task<PlayerGrainState> GetStateAsync() => Task.FromResult(state.State);

    public async Task<bool> TryLevelUpAsync()
    {
        var requiredXp = state.State.Level * 1000L;
        if (state.State.Experience < requiredXp)
            return false;

        var oldLevel = state.State.Level;
        state.State.Experience -= requiredXp;
        state.State.Level++;
        state.State.MaxHealth = 100 + (state.State.Level - 1) * 20;
        state.State.Health = state.State.MaxHealth;
        await state.WriteStateAsync();

        await eventBus.PublishAsync(new PlayerLevelUpEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            this.GetPrimaryKey(), oldLevel, state.State.Level));

        return true;
    }

    public async Task<bool> RespawnAsync()
    {
        if (state.State.Health > 0)
            return false;

        state.State.Health = state.State.MaxHealth;
        await state.WriteStateAsync();
        logger.LogInformation("Player {Id} respawned", this.GetPrimaryKey());
        return true;
    }
}
