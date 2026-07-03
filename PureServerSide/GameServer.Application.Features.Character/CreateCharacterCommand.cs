using Game.Shared.Core.DTOs;
using GameServer.Application.Core;
using GameServer.Application.Core.Behaviors;
using GameServer.Application.Core.CQRS;
using GameServer.Domain.Core;
using GameServer.Domain.Entities;
using GameServer.Grains.Interfaces;
using Orleans;

namespace GameServer.Application.Features.Character;

public sealed record CreateCharacterCommand(
    Guid AccountId,
    string Name,
    string Faction,
    string CharacterClass) : ICommand<CharacterCreateResultDto>;

public sealed record GetCharacterListQuery(Guid AccountId) : IQuery<CharacterListDto>, ICacheableQuery
{
    public string CacheKey => AccountDataCacheKeys.CharacterList(AccountId);
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(2);
}

public sealed record CreateCharacterFullCommand(CreateCharacterFullCommandDto Command) : ICommand<CharacterCreateFullResultDto>;

public sealed record SelectCharacterCommand(Guid AccountId, Guid CharacterId) : ICommand<SelectCharacterResultDto>;

public sealed record GetCharacterQuery(Guid CharacterId) : IQuery<PlayerDto?>;

public sealed record GainExperienceCommand(Guid PlayerId, long Amount) : ICommand<bool>;

public sealed class CreateCharacterHandler(
    IRepository<Player, Guid> playerRepository,
    IGrainFactory grainFactory,
    IAccountCharacterRegistry accountCharacterRegistry,
    ICacheService cacheService)
    : ICommandHandler<CreateCharacterCommand, CharacterCreateResultDto>
{
    public async Task<CharacterCreateResultDto> Handle(CreateCharacterCommand request, CancellationToken ct)
    {
        var player = Player.Create(Guid.NewGuid(), request.Name, request.Faction, request.CharacterClass);
        await playerRepository.AddAsync(player, ct);
        await accountCharacterRegistry.AddCharacterAsync(request.AccountId, player.Id, ct);
        await cacheService.RemoveAsync(AccountDataCacheKeys.CharacterList(request.AccountId), ct);

        // Initialize the Orleans Grain state
        var grain = grainFactory.GetGrain<IPlayerGrain>(player.Id);
        await grain.SetNameAsync(player.Name);

        return new CharacterCreateResultDto(true, player.Id, null);
    }
}

public sealed class GetCharacterListHandler(
    IRepository<Player, Guid> playerRepository,
    IAccountCharacterRegistry accountCharacterRegistry)
    : IQueryHandler<GetCharacterListQuery, CharacterListDto>
{
    public async Task<CharacterListDto> Handle(GetCharacterListQuery request, CancellationToken ct)
    {
        var account = await accountCharacterRegistry.GetAccountAsync(request.AccountId, ct);
        var characterIds = await accountCharacterRegistry.GetCharactersAsync(request.AccountId, ct);
        if (account?.LastSelectedCharacterId is Guid lastSelectedCharacterId)
        {
            characterIds = [
                .. characterIds.Where(id => id == lastSelectedCharacterId),
                .. characterIds.Where(id => id != lastSelectedCharacterId)
            ];
        }

        var characters = new List<CharacterSlotDto>(characterIds.Count);

        foreach (var characterId in characterIds)
        {
            var player = await playerRepository.GetByIdAsync(characterId, ct);
            if (player is null)
            {
                continue;
            }

            characters.Add(new CharacterSlotDto(
                player.Id,
                player.Name,
                player.Level,
                player.Faction,
                player.CharacterClass,
                player.CurrentWorldId,
                player.UpdatedAt,
                null,
                0));
        }

        return new CharacterListDto(request.AccountId, characters, 8);
    }
}

public sealed class CreateCharacterFullHandler(
    IRepository<Player, Guid> playerRepository,
    IGrainFactory grainFactory,
    IAccountCharacterRegistry accountCharacterRegistry,
    ICacheService cacheService,
    ISpawnPointAssigner? spawnPointAssigner = null)
    : ICommandHandler<CreateCharacterFullCommand, CharacterCreateFullResultDto>
{
    public async Task<CharacterCreateFullResultDto> Handle(CreateCharacterFullCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Command.Name))
        {
            return new CharacterCreateFullResultDto(false, null, [], "Character name is required.");
        }

        var mainCharacter = Player.Create(
            Guid.NewGuid(),
            request.Command.Name.Trim(),
            request.Command.Faction,
            request.Command.CharacterClass);

        // 分配出生位置（新手村散布）
        if (spawnPointAssigner is not null)
        {
            await spawnPointAssigner.AssignSpawnPositionAsync(mainCharacter, ct);
        }

        await playerRepository.AddAsync(mainCharacter, ct);
        await accountCharacterRegistry.AddCharacterAsync(request.Command.PlayerId, mainCharacter.Id, ct);

        var mainGrain = grainFactory.GetGrain<IPlayerGrain>(mainCharacter.Id);
        await mainGrain.SetNameAsync(mainCharacter.Name);
        var worldId = mainCharacter.CurrentWorldId ?? request.Command.StartingZone;
        if (!string.IsNullOrWhiteSpace(worldId))
        {
            await mainGrain.SetWorldAsync(worldId!);
            await mainGrain.SetPositionAsync(mainCharacter.LocalPositionX, mainCharacter.LocalPositionY, mainCharacter.LocalPositionZ, 0);
        }

        var squadMemberIds = new List<Guid>(request.Command.SquadMembers.Count);
        foreach (var squadMember in request.Command.SquadMembers.Take(request.Command.SquadMemberCount))
        {
            if (string.IsNullOrWhiteSpace(squadMember.Name))
            {
                continue;
            }

            var member = Player.Create(
                Guid.NewGuid(),
                squadMember.Name.Trim(),
                request.Command.Faction,
                squadMember.CharacterClass);

            // 队友也分配到同一出生区域附近
            if (spawnPointAssigner is not null)
            {
                await spawnPointAssigner.AssignSpawnPositionAsync(member, ct);
            }

            await playerRepository.AddAsync(member, ct);
            await accountCharacterRegistry.AddCharacterAsync(request.Command.PlayerId, member.Id, ct);

            var memberGrain = grainFactory.GetGrain<IPlayerGrain>(member.Id);
            await memberGrain.SetNameAsync(member.Name);
            var memberWorldId = member.CurrentWorldId ?? request.Command.StartingZone;
            if (!string.IsNullOrWhiteSpace(memberWorldId))
            {
                await memberGrain.SetWorldAsync(memberWorldId!);
                await memberGrain.SetPositionAsync(member.LocalPositionX, member.LocalPositionY, member.LocalPositionZ, 0);
            }

            squadMemberIds.Add(member.Id);
        }

        await cacheService.RemoveAsync(AccountDataCacheKeys.CharacterList(request.Command.PlayerId), ct);
        return new CharacterCreateFullResultDto(true, mainCharacter.Id, squadMemberIds, null);
    }
}

public sealed class SelectCharacterHandler(
    IRepository<Player, Guid> playerRepository,
    IAccountCharacterRegistry accountCharacterRegistry,
    ICacheService cacheService)
    : ICommandHandler<SelectCharacterCommand, SelectCharacterResultDto>
{
    private static readonly TimeSpan SessionDuration = TimeSpan.FromHours(12);

    public async Task<SelectCharacterResultDto> Handle(SelectCharacterCommand request, CancellationToken ct)
    {
        var ownsCharacter = await accountCharacterRegistry.OwnsCharacterAsync(request.AccountId, request.CharacterId, ct);
        if (!ownsCharacter)
        {
            return new SelectCharacterResultDto(false, null, null, "Character does not belong to this account.");
        }

        var player = await playerRepository.GetByIdAsync(request.CharacterId, ct);
        if (player is null)
        {
            return new SelectCharacterResultDto(false, null, null, "Character not found.");
        }

        await accountCharacterRegistry.UpdateLastSelectedCharacterAsync(request.AccountId, request.CharacterId, ct);
        await cacheService.RemoveAsync(AccountDataCacheKeys.CharacterList(request.AccountId), ct);

        var token = await cacheService.GetAsync<string>(AccountDataCacheKeys.SessionPrincipal(request.AccountId), ct);
        if (!string.IsNullOrWhiteSpace(token))
        {
            var session = await cacheService.GetAsync<LoginSessionCacheEntry>(AccountDataCacheKeys.SessionToken(token), ct);
            if (session is not null)
            {
                if (session.CharacterId.HasValue && session.CharacterId.Value != request.CharacterId)
                {
                    await cacheService.RemoveAsync(AccountDataCacheKeys.SessionPrincipal(session.CharacterId.Value), ct);
                }

                var updatedSession = session with
                {
                    CharacterId = request.CharacterId,
                    LastActivityAt = DateTime.UtcNow
                };

                await cacheService.SetAsync(AccountDataCacheKeys.SessionToken(token), updatedSession, SessionDuration, ct);
                await cacheService.SetAsync(AccountDataCacheKeys.SessionPrincipal(request.AccountId), token, SessionDuration, ct);
                await cacheService.SetAsync(AccountDataCacheKeys.SessionPrincipal(request.CharacterId), token, SessionDuration, ct);
            }
        }

        var dto = new PlayerDto(player.Id, player.Name, player.Level, player.Faction, player.Experience);
        return new SelectCharacterResultDto(true, player.Id, dto, null);
    }
}

public sealed class GetCharacterHandler(
    IGrainFactory grainFactory)
    : IQueryHandler<GetCharacterQuery, PlayerDto?>
{
    public async Task<PlayerDto?> Handle(GetCharacterQuery request, CancellationToken ct)
    {
        var grain = grainFactory.GetGrain<IPlayerGrain>(request.CharacterId);
        var state = await grain.GetStateAsync();

        if (string.IsNullOrEmpty(state.Name)) return null;

        return new PlayerDto(
            request.CharacterId,
            state.Name,
            state.Level,
            state.Faction,
            state.Experience);
    }
}

public sealed class GainExperienceHandler(
    IGrainFactory grainFactory)
    : ICommandHandler<GainExperienceCommand, bool>
{
    public async Task<bool> Handle(GainExperienceCommand request, CancellationToken ct)
    {
        var grain = grainFactory.GetGrain<IPlayerGrain>(request.PlayerId);
        return await grain.GainExperienceAsync(request.Amount);
    }
}
