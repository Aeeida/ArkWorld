namespace Ark.World.Data;

public readonly record struct PersistedTerrainModification(
    string ModType,
    string? ChunkKey,
    long SequenceTick,
    float X,
    float Z,
    float RadiusX,
    float RadiusZ,
    float TargetHeight,
    string? MetadataJson);
