namespace GameServer.Application.Core;

public sealed record AccountInfo(
    Guid AccountId,
    string AccountName,
    Guid? LastSelectedCharacterId = null);

public sealed record AccountAuthenticationResult(
    bool Success,
    AccountInfo? Account,
    string? ErrorMessage,
    bool Created = false);

public interface IAccountCharacterRegistry
{
    Task<AccountAuthenticationResult> AuthenticateAsync(string accountName, string passwordHash, CancellationToken ct = default);
    Task<AccountInfo?> GetAccountAsync(Guid accountId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetCharactersAsync(Guid accountId, CancellationToken ct = default);
    Task AddCharacterAsync(Guid accountId, Guid characterId, CancellationToken ct = default);
    Task<bool> OwnsCharacterAsync(Guid accountId, Guid characterId, CancellationToken ct = default);
    Task UpdateLastSelectedCharacterAsync(Guid accountId, Guid characterId, CancellationToken ct = default);
}
