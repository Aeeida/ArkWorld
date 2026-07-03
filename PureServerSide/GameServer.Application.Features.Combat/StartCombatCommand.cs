using Game.Shared.Core.DTOs;
using GameServer.Application.Core.CQRS;
using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;

namespace GameServer.Application.Features.Combat;

public sealed record StartCombatCommand(Guid AttackerId, Guid DefenderId) : ICommand<CombatResultDto>;

public sealed record ApplyDamageCommand(Guid AttackerId, Guid TargetId, double Damage) : ICommand<CombatResultDto>;

public sealed record AttackCommand(Guid AttackerId, Guid TargetId, string? SkillId) : ICommand<AttackResultDto>;

public sealed record UseSkillCommand(Guid PlayerId, string SkillId, Guid? TargetId) : ICommand<UseSkillResultDto>;

public sealed record GetBattleStatusQuery(Guid PlayerId) : IQuery<BattleStatusDto>;

public sealed record CommandFleetAttackCommand(Guid FleetId, Guid TargetFleetId) : ICommand<FleetBattleCommandResultDto>;

public sealed record RespawnCommand(Guid PlayerId) : ICommand<RespawnResultDto>;

public sealed class StartCombatHandler(
    IGrainFactory grainFactory,
    ILogger<StartCombatHandler> logger)
    : ICommandHandler<StartCombatCommand, CombatResultDto>
{
    public async Task<CombatResultDto> Handle(StartCombatCommand request, CancellationToken ct)
    {
        var attackerGrain = grainFactory.GetGrain<IPlayerGrain>(request.AttackerId);
        var defenderGrain = grainFactory.GetGrain<IPlayerGrain>(request.DefenderId);

        var attackerState = await attackerGrain.GetStateAsync();
        var defenderState = await defenderGrain.GetStateAsync();

        if (attackerState.Health <= 0)
            return new CombatResultDto(false, request.AttackerId, request.DefenderId, 0, "Attacker is dead.");

        if (defenderState.Health <= 0)
            return new CombatResultDto(false, request.AttackerId, request.DefenderId, 0, "Target is already dead.");

        // Base damage calculation: level-based + class modifier
        var baseDamage = 10.0 + attackerState.Level * 2.0;
        await defenderGrain.TakeDamageAsync(baseDamage);

        logger.LogInformation("Player {Attacker} dealt {Damage} to {Defender}",
            request.AttackerId, baseDamage, request.DefenderId);

        return new CombatResultDto(true, request.AttackerId, request.DefenderId, baseDamage, null);
    }
}

public sealed class ApplyDamageHandler(
    IGrainFactory grainFactory)
    : ICommandHandler<ApplyDamageCommand, CombatResultDto>
{
    public async Task<CombatResultDto> Handle(ApplyDamageCommand request, CancellationToken ct)
    {
        var targetGrain = grainFactory.GetGrain<IPlayerGrain>(request.TargetId);
        await targetGrain.TakeDamageAsync(request.Damage);
        return new CombatResultDto(true, request.AttackerId, request.TargetId, request.Damage, null);
    }
}

public sealed class AttackHandler(
    IGrainFactory grainFactory,
    ILogger<AttackHandler> logger)
    : ICommandHandler<AttackCommand, AttackResultDto>
{
    public async Task<AttackResultDto> Handle(AttackCommand request, CancellationToken ct)
    {
        var attackerGrain = grainFactory.GetGrain<IPlayerGrain>(request.AttackerId);
        var targetGrain = grainFactory.GetGrain<IPlayerGrain>(request.TargetId);

        var attackerState = await attackerGrain.GetStateAsync();
        var targetState = await targetGrain.GetStateAsync();

        if (attackerState.Health <= 0)
            return new AttackResultDto(false, request.AttackerId, request.TargetId, 0, false, "Attacker is dead.");

        if (targetState.Health <= 0)
            return new AttackResultDto(false, request.AttackerId, request.TargetId, 0, true, "Target is already dead.");

        var baseDamage = 10.0 + attackerState.Level * 2.0;
        if (request.SkillId is not null)
            baseDamage *= 1.5; // Skill multiplier

        await targetGrain.TakeDamageAsync(baseDamage);
        var newState = await targetGrain.GetStateAsync();
        var destroyed = newState.Health <= 0;

        logger.LogInformation("Player {Attacker} attacked {Target} for {Damage}, destroyed={Destroyed}",
            request.AttackerId, request.TargetId, baseDamage, destroyed);

        return new AttackResultDto(true, request.AttackerId, request.TargetId, baseDamage, destroyed, null);
    }
}

public sealed class UseSkillHandler(
    IGrainFactory grainFactory,
    ILogger<UseSkillHandler> logger)
    : ICommandHandler<UseSkillCommand, UseSkillResultDto>
{
    public async Task<UseSkillResultDto> Handle(UseSkillCommand request, CancellationToken ct)
    {
        var skillGrain = grainFactory.GetGrain<ISkillGrain>(request.PlayerId);
        var skillState = await skillGrain.GetStateAsync();

        if (!skillState.LearnedSkills.TryGetValue(request.SkillId, out var level))
            return new UseSkillResultDto(false, request.PlayerId, request.SkillId, 0, 0, "Skill not learned");

        var effectValue = level * 15.0;
        var cooldown = 5.0 - level * 0.2;

        if (request.TargetId.HasValue)
        {
            var targetGrain = grainFactory.GetGrain<IPlayerGrain>(request.TargetId.Value);
            await targetGrain.TakeDamageAsync(effectValue);
        }

        logger.LogInformation("Player {PlayerId} used skill {SkillId} effect={Effect}",
            request.PlayerId, request.SkillId, effectValue);

        return new UseSkillResultDto(true, request.PlayerId, request.SkillId, effectValue, cooldown, null);
    }
}

public sealed class GetBattleStatusHandler : IQueryHandler<GetBattleStatusQuery, BattleStatusDto>
{
    public Task<BattleStatusDto> Handle(GetBattleStatusQuery request, CancellationToken ct)
    {
        // TODO: Delegate to IBattleGrain
        return Task.FromResult(new BattleStatusDto(
            Guid.Empty, "Idle", [], 0, DateTime.UtcNow));
    }
}

public sealed class CommandFleetAttackHandler(
    IGrainFactory grainFactory,
    ILogger<CommandFleetAttackHandler> logger)
    : ICommandHandler<CommandFleetAttackCommand, FleetBattleCommandResultDto>
{
    public async Task<FleetBattleCommandResultDto> Handle(CommandFleetAttackCommand request, CancellationToken ct)
    {
        var fleetGrain = grainFactory.GetGrain<IFleetGrain>(request.FleetId);
        var fleetState = await fleetGrain.GetStateAsync();

        if (string.IsNullOrEmpty(fleetState.Name))
            return new FleetBattleCommandResultDto(false, request.FleetId, request.TargetFleetId, "Fleet not found");

        logger.LogInformation("Fleet {FleetId} commanded to attack {TargetFleetId}",
            request.FleetId, request.TargetFleetId);

        return new FleetBattleCommandResultDto(true, request.FleetId, request.TargetFleetId, null);
    }
}

public sealed class RespawnHandler(
    IGrainFactory grainFactory,
    ILogger<RespawnHandler> logger)
    : ICommandHandler<RespawnCommand, RespawnResultDto>
{
    public async Task<RespawnResultDto> Handle(RespawnCommand request, CancellationToken ct)
    {
        var playerGrain = grainFactory.GetGrain<IPlayerGrain>(request.PlayerId);
        var result = await playerGrain.RespawnAsync();

        if (!result)
            return new RespawnResultDto(false, null, "Player is not dead");

        var state = await playerGrain.GetStateAsync();
        logger.LogInformation("Player {PlayerId} respawned in {World}", request.PlayerId, state.CurrentWorldId);

        return new RespawnResultDto(true, state.CurrentWorldId, null);
    }
}
