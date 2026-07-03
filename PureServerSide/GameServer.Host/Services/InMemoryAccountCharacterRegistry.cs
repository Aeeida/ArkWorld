using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using GameServer.Application.Core;

namespace GameServer.Host.Services;

public sealed class InMemoryAccountCharacterRegistry : IAccountCharacterRegistry
{
    private readonly ConcurrentDictionary<Guid, AccountState> _accounts = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, byte>> _accountCharacters = new();

    public Task<AccountAuthenticationResult> AuthenticateAsync(string accountName, string passwordHash, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var normalized = NormalizeAccountName(accountName);
        var accountId = CreateDeterministicGuid(normalized);
        var created = _accounts.TryAdd(accountId, new AccountState(normalized, passwordHash, null));
        _accountCharacters.TryAdd(accountId, []);

        if (!created)
        {
            var account = _accounts[accountId];
            if (!string.Equals(account.PasswordHash, passwordHash, StringComparison.Ordinal))
            {
                return Task.FromResult(new AccountAuthenticationResult(false, null, "Invalid credentials."));
            }

            return Task.FromResult(new AccountAuthenticationResult(
                true,
                new AccountInfo(accountId, account.Name, account.LastSelectedCharacterId),
                null,
                false));
        }

        return Task.FromResult(new AccountAuthenticationResult(
            true,
            new AccountInfo(accountId, normalized),
            null,
            true));
    }

    public Task<AccountInfo?> GetAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<AccountInfo?>(
            _accounts.TryGetValue(accountId, out var account)
                ? new AccountInfo(accountId, account.Name, account.LastSelectedCharacterId)
                : null);
    }

    public Task<IReadOnlyList<Guid>> GetCharactersAsync(Guid accountId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (_accountCharacters.TryGetValue(accountId, out var characters))
        {
            return Task.FromResult<IReadOnlyList<Guid>>([.. characters.Keys]);
        }

        return Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    public Task AddCharacterAsync(Guid accountId, Guid characterId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var characters = _accountCharacters.GetOrAdd(accountId, _ => []);
        characters.TryAdd(characterId, 0);
        return Task.CompletedTask;
    }

    public Task<bool> OwnsCharacterAsync(Guid accountId, Guid characterId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var ownsCharacter = _accountCharacters.TryGetValue(accountId, out var characters)
            && characters.ContainsKey(characterId);
        return Task.FromResult(ownsCharacter);
    }

    public Task UpdateLastSelectedCharacterAsync(Guid accountId, Guid characterId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (_accounts.TryGetValue(accountId, out var account))
        {
            _accounts[accountId] = account with { LastSelectedCharacterId = characterId };
        }

        return Task.CompletedTask;
    }

    private static string NormalizeAccountName(string accountName)
        => accountName.Trim().ToLowerInvariant();

    private static Guid CreateDeterministicGuid(string value)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(value), hash);
        return new Guid(hash[..16]);
    }

    private sealed record AccountState(string Name, string PasswordHash, Guid? LastSelectedCharacterId);
}
