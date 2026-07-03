using Game.Shared.Core.DTOs;
using GameServer.Application.Core.CQRS;
using GameServer.Application.Core;
using GameServer.Application.Core.Behaviors;
using GameServer.Grains.Interfaces;
using Orleans;

namespace GameServer.Application.Features.Login;

public sealed class LoginCommandHandler(
    IAccountCharacterRegistry accountCharacterRegistry,
    ICacheService cacheService)
    : ICommandHandler<LoginCommand, LoginResultDto>
{
    private static readonly TimeSpan SessionDuration = TimeSpan.FromHours(12);

    public async Task<LoginResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AccountId))
        {
            return new LoginResultDto(false, null, null, "Invalid credentials.");
        }

        var authResult = await accountCharacterRegistry.AuthenticateAsync(request.AccountId, request.PasswordHash, cancellationToken);
        if (!authResult.Success || authResult.Account is null)
        {
            return new LoginResultDto(false, null, null, authResult.ErrorMessage ?? "Invalid credentials.");
        }

        var account = authResult.Account;
        var characterIds = await accountCharacterRegistry.GetCharactersAsync(account.AccountId, cancellationToken);
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var now = DateTime.UtcNow;
        var session = new LoginSessionCacheEntry(token, account.AccountId, account.AccountName, account.LastSelectedCharacterId, now, now);
        await cacheService.SetAsync(AccountDataCacheKeys.SessionToken(token), session, SessionDuration, cancellationToken);
        await cacheService.SetAsync(AccountDataCacheKeys.SessionPrincipal(account.AccountId), token, SessionDuration, cancellationToken);
        if (account.LastSelectedCharacterId.HasValue)
        {
            await cacheService.SetAsync(AccountDataCacheKeys.SessionPrincipal(account.LastSelectedCharacterId.Value), token, SessionDuration, cancellationToken);
        }

        var dto = new PlayerDto(account.AccountId, account.AccountName, characterIds.Count, "Account", 0);

        return new LoginResultDto(true, token, dto, null);
    }
}

public sealed class LogoutCommandHandler(
    ICacheService cacheService) : ICommandHandler<LogoutCommand, LogoutResultDto>
{
    public async Task<LogoutResultDto> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var principalKey = AccountDataCacheKeys.SessionPrincipal(request.PlayerId);
        var token = await cacheService.GetAsync<string>(principalKey, cancellationToken);

        await cacheService.RemoveAsync(principalKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(token))
        {
            var session = await cacheService.GetAsync<LoginSessionCacheEntry>(AccountDataCacheKeys.SessionToken(token), cancellationToken);
            await cacheService.RemoveAsync(AccountDataCacheKeys.SessionToken(token), cancellationToken);

            if (session is not null)
            {
                await cacheService.RemoveAsync(AccountDataCacheKeys.SessionPrincipal(session.AccountId), cancellationToken);
                if (session.CharacterId.HasValue)
                {
                    await cacheService.RemoveAsync(AccountDataCacheKeys.SessionPrincipal(session.CharacterId.Value), cancellationToken);
                }
            }
        }

        return new LogoutResultDto(true, null);
    }
}

public sealed class JoinWorldCommandHandler(
    IGrainFactory grainFactory)
    : ICommandHandler<JoinWorldCommand, JoinWorldResultDto>
{
    public async Task<JoinWorldResultDto> Handle(JoinWorldCommand request, CancellationToken ct)
    {
        var playerGrain = grainFactory.GetGrain<IPlayerGrain>(request.PlayerId);
        var currentWorld = await playerGrain.GetWorldAsync();
        if (currentWorld is not null)
        {
            var oldSystem = grainFactory.GetGrain<ISolarSystemGrain>(currentWorld);
            await oldSystem.PlayerLeftAsync(request.PlayerId);
        }

        await playerGrain.SetWorldAsync(request.WorldId);
        var solarSystem = grainFactory.GetGrain<ISolarSystemGrain>(request.WorldId);
        await solarSystem.PlayerEnteredAsync(request.PlayerId);

        var state = await solarSystem.GetStateAsync();
        return new JoinWorldResultDto(true, request.WorldId, state.PlayerIds.Count, null);
    }
}
