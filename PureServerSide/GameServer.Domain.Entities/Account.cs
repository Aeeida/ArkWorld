using GameServer.Domain.Core;

namespace GameServer.Domain.Entities;

public class Account : AggregateRoot<Guid>
{
    public string AccountName
    {
        get => field;
        private set => field = !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("Account name cannot be empty.");
    } = string.Empty;

    public string NormalizedName
    {
        get => field;
        private set => field = !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("Normalized account name cannot be empty.");
    } = string.Empty;

    public string PasswordHash
    {
        get => field;
        private set => field = !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("Password hash cannot be empty.");
    } = string.Empty;

    public Guid? LastSelectedCharacterId { get; private set; }
    public DateTime LastLoginAt { get; private set; } = DateTime.UtcNow;

    public static Account Create(Guid id, string accountName, string normalizedName, string passwordHash)
        => new()
        {
            Id = id,
            AccountName = accountName,
            NormalizedName = normalizedName,
            PasswordHash = passwordHash,
            LastLoginAt = DateTime.UtcNow
        };

    public bool PasswordMatches(string passwordHash) =>
        string.Equals(PasswordHash, passwordHash, StringComparison.Ordinal);

    public void UpdateLogin(string passwordHash)
    {
        PasswordHash = passwordHash;
        LastLoginAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SelectCharacter(Guid characterId)
    {
        LastSelectedCharacterId = characterId;
        UpdatedAt = DateTime.UtcNow;
    }
}

public class AccountCharacter
{
    public Guid AccountId { get; init; }
    public Guid CharacterId { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
