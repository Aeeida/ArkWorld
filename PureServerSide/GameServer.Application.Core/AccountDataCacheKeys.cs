namespace GameServer.Application.Core;

public static class AccountDataCacheKeys
{
    public static string AccountId(Guid accountId) => $"account:id:{accountId:N}";
    public static string AccountName(string normalizedName) => $"account:name:{normalizedName}";
    public static string AccountCharacters(Guid accountId) => $"account:characters:{accountId:N}";
    public static string CharacterList(Guid accountId) => $"character:list:{accountId:N}";
    public static string SessionToken(string token) => $"session:token:{token}";
    public static string SessionPrincipal(Guid principalId) => $"session:principal:{principalId:N}";
}

public sealed record LoginSessionCacheEntry(
    string Token,
    Guid AccountId,
    string AccountName,
    Guid? CharacterId,
    DateTime IssuedAt,
    DateTime LastActivityAt);
