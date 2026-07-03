using System.Numerics;
using System.Text.Json;

namespace Game.Shared.Core.Space;

public readonly record struct RocketEngineForceProfile(float Thrust, Vector3 LocalReactionDirection, bool IsPrimaryEngine);

public static class RocketEngineForceCatalog
{
    private static readonly IReadOnlyDictionary<int, RocketEngineForceProfile> Profiles = new Dictionary<int, RocketEngineForceProfile>
    {
        [20] = new(200f, Vector3.UnitY, true),
        [21] = new(600f, Vector3.UnitY, true),
        [22] = new(1400f, Vector3.UnitY, true),
        [23] = new(60f, Vector3.UnitY, true),
        [30] = new(300f, Vector3.UnitY, true),
        [31] = new(900f, Vector3.UnitY, true),
        [62] = new(2f, Vector3.UnitY, false),
    };

    public static bool TryGetReactionDirection(IEnumerable<int> installedPartIds, out Vector3 localReactionDirection)
    {
        Vector3 weighted = Vector3.Zero;
        float totalThrust = 0f;

        foreach (var partId in installedPartIds)
        {
            if (!Profiles.TryGetValue(partId, out var profile) || !profile.IsPrimaryEngine)
                continue;

            weighted += Vector3.Normalize(profile.LocalReactionDirection) * profile.Thrust;
            totalThrust += profile.Thrust;
        }

        if (totalThrust <= 0f || weighted.LengthSquared() <= 1e-6f)
        {
            localReactionDirection = Vector3.UnitY;
            return false;
        }

        localReactionDirection = Vector3.Normalize(weighted / totalThrust);
        return true;
    }

    public static bool TryGetReactionDirectionFromConfigJson(string? rocketConfigJson, out Vector3 localReactionDirection)
    {
        localReactionDirection = Vector3.UnitY;
        if (string.IsNullOrWhiteSpace(rocketConfigJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(rocketConfigJson);
            if (!doc.RootElement.TryGetProperty("InstalledPartIds", out var idsElement) || idsElement.ValueKind != JsonValueKind.Array)
                return false;

            var installedPartIds = new List<int>();
            foreach (var item in idsElement.EnumerateArray())
            {
                if (item.TryGetInt32(out var partId))
                    installedPartIds.Add(partId);
            }

            return TryGetReactionDirection(installedPartIds, out localReactionDirection);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
