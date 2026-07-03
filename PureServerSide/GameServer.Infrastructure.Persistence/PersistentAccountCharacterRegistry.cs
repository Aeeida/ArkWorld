using GameServer.Application.Core;
using GameServer.Application.Core.Behaviors;
using GameServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Persistence;

public sealed class PersistentAccountCharacterRegistry(
    GameDbContext context,
    ICacheService cacheService) : IAccountCharacterRegistry
{
    private static readonly TimeSpan AccountCacheDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CharacterOwnershipCacheDuration = TimeSpan.FromMinutes(5);

    public async Task<AccountAuthenticationResult> AuthenticateAsync(string accountName, string passwordHash, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var trimmedName = accountName.Trim();
        var normalizedName = NormalizeAccountName(trimmedName);
        var cachedAccount = await cacheService.GetAsync<AccountCacheEntry>(AccountDataCacheKeys.AccountName(normalizedName), ct);
        if (cachedAccount is not null)
        {
            if (!string.Equals(cachedAccount.PasswordHash, passwordHash, StringComparison.Ordinal))
            {
                return new AccountAuthenticationResult(false, null, "Invalid credentials.");
            }

            return new AccountAuthenticationResult(
                true,
                cachedAccount.ToAccountInfo(),
                null,
                false);
        }

        var account = await context.Accounts
            .SingleOrDefaultAsync(x => x.NormalizedName == normalizedName, ct);

        if (account is null)
        {
            account = Account.Create(Guid.NewGuid(), trimmedName, normalizedName, passwordHash);
            await context.Accounts.AddAsync(account, ct);
            await context.SaveChangesAsync(ct);

            var createdEntry = AccountCacheEntry.FromAccount(account);
            await CacheAccountAsync(createdEntry, ct);
            return new AccountAuthenticationResult(true, createdEntry.ToAccountInfo(), null, true);
        }

        if (!account.PasswordMatches(passwordHash))
        {
            return new AccountAuthenticationResult(false, null, "Invalid credentials.");
        }

        account.UpdateLogin(passwordHash);
        await context.SaveChangesAsync(ct);

        var existingEntry = AccountCacheEntry.FromAccount(account);
        await CacheAccountAsync(existingEntry, ct);
        return new AccountAuthenticationResult(true, existingEntry.ToAccountInfo(), null, false);
    }

    public async Task<AccountInfo?> GetAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var cachedAccount = await cacheService.GetAsync<AccountCacheEntry>(AccountDataCacheKeys.AccountId(accountId), ct);
        if (cachedAccount is not null)
        {
            return cachedAccount.ToAccountInfo();
        }

        var account = await context.Accounts
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == accountId, ct);
        if (account is null)
        {
            return null;
        }

        var entry = AccountCacheEntry.FromAccount(account);
        await CacheAccountAsync(entry, ct);
        return entry.ToAccountInfo();
    }

    public async Task<IReadOnlyList<Guid>> GetCharactersAsync(Guid accountId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var cacheKey = AccountDataCacheKeys.AccountCharacters(accountId);
        var cachedCharacters = await cacheService.GetAsync<List<Guid>>(cacheKey, ct);
        if (cachedCharacters is not null)
        {
            return cachedCharacters;
        }

        var characterIds = await context.AccountCharacters
            .AsNoTracking()
            .Where(x => x.AccountId == accountId)
            .Select(x => x.CharacterId)
            .ToListAsync(ct);

        await cacheService.SetAsync(cacheKey, characterIds, CharacterOwnershipCacheDuration, ct);
        return characterIds;
    }

    public async Task AddCharacterAsync(Guid accountId, Guid characterId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var exists = await context.AccountCharacters
            .AnyAsync(x => x.AccountId == accountId && x.CharacterId == characterId, ct);
        if (exists)
        {
            return;
        }

        await context.AccountCharacters.AddAsync(new AccountCharacter
        {
            AccountId = accountId,
            CharacterId = characterId
        }, ct);
        await context.SaveChangesAsync(ct);
        await InvalidateCharacterCacheAsync(accountId, ct);
    }

    public async Task<bool> OwnsCharacterAsync(Guid accountId, Guid characterId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var characterIds = await GetCharactersAsync(accountId, ct);
        return characterIds.Contains(characterId);
    }

    public async Task UpdateLastSelectedCharacterAsync(Guid accountId, Guid characterId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var account = await context.Accounts.SingleOrDefaultAsync(x => x.Id == accountId, ct);
        if (account is null)
        {
            return;
        }

        account.SelectCharacter(characterId);
        await context.SaveChangesAsync(ct);
        await CacheAccountAsync(AccountCacheEntry.FromAccount(account), ct);
    }

    private async Task CacheAccountAsync(AccountCacheEntry account, CancellationToken ct)
    {
        await cacheService.SetAsync(AccountDataCacheKeys.AccountId(account.AccountId), account, AccountCacheDuration, ct);
        await cacheService.SetAsync(AccountDataCacheKeys.AccountName(account.NormalizedName), account, AccountCacheDuration, ct);
    }

    private async Task InvalidateCharacterCacheAsync(Guid accountId, CancellationToken ct)
    {
        await cacheService.RemoveAsync(AccountDataCacheKeys.AccountCharacters(accountId), ct);
    }

    private static string NormalizeAccountName(string accountName) =>
        accountName.Trim().ToLowerInvariant();

    private sealed record AccountCacheEntry(
        Guid AccountId,
        string AccountName,
        string NormalizedName,
        string PasswordHash,
        Guid? LastSelectedCharacterId)
    {
        public static AccountCacheEntry FromAccount(Account account) =>
            new(account.Id, account.AccountName, account.NormalizedName, account.PasswordHash, account.LastSelectedCharacterId);

        public AccountInfo ToAccountInfo() =>
            new(AccountId, AccountName, LastSelectedCharacterId);
    }
}
