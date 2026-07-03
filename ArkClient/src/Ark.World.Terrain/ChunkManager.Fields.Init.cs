using Godot;
using System.Collections.Concurrent;
using System.Threading;
using Ark.World.Core;
using Ark.World.Data;

namespace Ark.World.Terrain;

public sealed partial class ChunkManager
{
    public string SystemId => "ChunkManager";

    private readonly HeightfieldGenerator _generator;
    private readonly ModificationLog _modLog;
    private readonly Node3D _root;
    private readonly Dictionary<ChunkCoord, LoadedChunk> _chunks = new();
    private ChunkCoord _lastPlayerChunk;
    private float _lastCameraAltitude;
    private bool _initialized;

    private const float UpdateCooldown = 0.3f;
    private const float HighSpeedCooldown = 0.1f;
    private float _cooldownTimer;
    private float _lastDeltaTime;

    private Vector3 _lastCameraPos;
    private Vector3 _cameraVelocity;
    private const float HighSpeedThreshold = 50f;
    private const float PredictionLookahead = 3f;

    private static readonly int MaxWorkers = Math.Max(1, System.Environment.ProcessorCount - 2);
    private const int MaxVisualsPerFrame = 16;
    private const int MaxUnloadsPerFrame = 8;
    private const int MaxLoadRadius = 20;

    private readonly HashSet<ChunkCoord> _generating = new();
    private readonly ConcurrentQueue<GeneratedChunkResult> _readyQueue = new();
    private readonly Queue<(ChunkCoord coord, int lodStep)> _pendingQueue = new();
    private readonly Queue<ChunkCoord> _unloadQueue = new();
    private int _activeWorkers;

    public event Action<HeightfieldChunk>? OnChunkLoaded;
    public event Action<ChunkCoord>? OnChunkUnloaded;

    private readonly List<(ChunkCoord center, int radius)> _anchoredRegions = new();

    public void AddAnchoredRegion(float worldX, float worldZ, int chunkRadius = 3)
    {
        var coord = ChunkCoord.FromWorldPos(worldX, worldZ, WorldConstants.ChunkSize);
        for (int i = 0; i < _anchoredRegions.Count; i++)
        {
            if (_anchoredRegions[i].center == coord)
            {
                _anchoredRegions[i] = (coord, chunkRadius);
                return;
            }
        }
        _anchoredRegions.Add((coord, chunkRadius));
    }

    public void RemoveAnchoredRegion(float worldX, float worldZ)
    {
        var coord = ChunkCoord.FromWorldPos(worldX, worldZ, WorldConstants.ChunkSize);
        _anchoredRegions.RemoveAll(r => r.center == coord);
    }

    public void ClearAnchoredRegions() => _anchoredRegions.Clear();

    public void UpdateAnchoredRegion(int index, float worldX, float worldZ)
    {
        if (index < 0 || index >= _anchoredRegions.Count) return;
        var coord = ChunkCoord.FromWorldPos(worldX, worldZ, WorldConstants.ChunkSize);
        _anchoredRegions[index] = (coord, _anchoredRegions[index].radius);
    }

    private bool IsInAnchoredRegion(ChunkCoord coord)
    {
        foreach (var (center, radius) in _anchoredRegions)
        {
            if (coord.ChebyshevDistance(center) <= radius)
                return true;
        }
        return false;
    }

    public int LoadedChunkCount => _chunks.Count;
    public Node3D SceneRoot => _root;

    public IReadOnlyDictionary<ChunkCoord, HeightfieldChunk> LoadedChunks
    {
        get
        {
            var result = new Dictionary<ChunkCoord, HeightfieldChunk>(_chunks.Count);
            foreach (var (coord, loaded) in _chunks)
                result[coord] = loaded.Data;
            return result;
        }
    }

    public ChunkManager(HeightfieldGenerator generator, ModificationLog modLog)
    {
        _generator = generator;
        _modLog = modLog;
        _root = new Node3D { Name = "TerrainChunks" };
    }

    public void Initialize(WorldSeed seed)
    {
        _initialized = true;
        var spawnChunk = ChunkCoord.FromWorldPos(WorldConstants.SpawnX, WorldConstants.SpawnZ, WorldConstants.ChunkSize);
        InitialLoadChunks(spawnChunk);
        _lastPlayerChunk = spawnChunk;
    }

    public void Shutdown()
    {
        _pendingQueue.Clear();
        _unloadQueue.Clear();
        _generating.Clear();
        while (_readyQueue.TryDequeue(out _)) { }

        foreach (var coord in _chunks.Keys)
            OnChunkUnloaded?.Invoke(coord);

        foreach (var (_, loaded) in _chunks)
        {
            if (loaded.SceneNode != null)
            {
                if (loaded.SceneNode.GetParent() != null)
                    loaded.SceneNode.GetParent().RemoveChild(loaded.SceneNode);
                loaded.SceneNode.Free();
                loaded.SceneNode = null;
            }
        }
        _chunks.Clear();
        _lastPlayerChunk = default;
        _initialized = false;
    }
}
