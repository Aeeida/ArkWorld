using System.Security.Cryptography;

namespace Game.Shared.Core.Utils;

public static class IdGenerator
{
    public static Guid NewSequentialId()
    {
        var timestamp = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
        var random = new byte[8];
        RandomNumberGenerator.Fill(random);

        var guid = new byte[16];
        Buffer.BlockCopy(timestamp, 0, guid, 0, 8);
        Buffer.BlockCopy(random, 0, guid, 8, 8);
        return new Guid(guid);
    }

    public static string NewShortId() =>
        Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("/", "_").Replace("+", "-")[..22];
}

public static class TimeUtils
{
    public static long UnixNowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public static long UnixNowSec => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public static DateTime FromUnixMs(long ms) =>
        DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;

    public static TimeSpan Since(DateTime utc) => DateTime.UtcNow - utc;

    public static bool HasExpired(DateTime expiresAt) => DateTime.UtcNow >= expiresAt;
}

public static class GameConstants
{
    public const int MaxPlayerLevel = 100;
    public const int MaxFleetSize = 256;
    public const int MaxGuildMembers = 500;
    public const int MaxInventorySlots = 200;
    public const int MarketOrderDurationDays = 90;
    public const double BaseHealth = 100.0;
    public const double HealthPerLevel = 20.0;
    public const long BaseXpPerLevel = 1000;
}
