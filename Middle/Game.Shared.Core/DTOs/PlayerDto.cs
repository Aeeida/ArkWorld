using MessagePack;

namespace Game.Shared.Core.DTOs;

[MessagePackObject]
public sealed record PlayerDto(
    [property: Key(0)] Guid Id,
    [property: Key(1)] string Name,
    [property: Key(2)] int Level,
    [property: Key(3)] string Faction,
    [property: Key(4)] long Experience = 0,
    [property: Key(5)] string? GuildName = null);

[MessagePackObject]
public sealed record LoginResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string? Token,
    [property: Key(2)] PlayerDto? Player,
    [property: Key(3)] string? ErrorMessage);

[MessagePackObject]
public sealed record CharacterCreateResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] Guid? CharacterId,
    [property: Key(2)] string? ErrorMessage);

[MessagePackObject]
public sealed record LogoutResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string? ErrorMessage);

[MessagePackObject]
public sealed record JoinWorldResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string? WorldId,
    [property: Key(2)] int OnlinePlayerCount,
    [property: Key(3)] string? ErrorMessage,
    [property: Key(4)] long TerrainSeed = 0,
    [property: Key(5)] string? BiomeId = null,
    [property: Key(6)] byte WeatherId = 0,
    [property: Key(7)] float WeatherIntensity = 0f,
    [property: Key(8)] float TimeOfDay = 0f,
    [property: Key(9)] float SpawnX = 0f,
    [property: Key(10)] float SpawnY = 0f,
    [property: Key(11)] float SpawnZ = 0f);

[MessagePackObject]
public sealed record LevelUpResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] int NewLevel,
    [property: Key(2)] int AttributePointsGained,
    [property: Key(3)] string? ErrorMessage);

[MessagePackObject]
public sealed record AttributeSetDto(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] int Strength,
    [property: Key(2)] int Agility,
    [property: Key(3)] int Intelligence,
    [property: Key(4)] int Stamina,
    [property: Key(5)] int Luck,
    [property: Key(6)] int UnspentPoints);

[MessagePackObject]
public sealed record RespawnResultDto(
    [property: Key(0)] bool Success,
    [property: Key(1)] string? SpawnLocationId,
    [property: Key(2)] string? ErrorMessage);
