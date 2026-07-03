using Godot;
using System.Collections.Concurrent;
using System.Threading;
using Ark.World.Core;
using Ark.World.Data;

namespace Ark.World.Terrain;

public sealed partial class ChunkManager
{
    public void UpdateCameraPosition(Vector3 cameraWorldPos, float cameraAltitude)
    {
        if (!_initialized) return;

        if (_lastDeltaTime > 0.001f)
        {
            var rawVel = (cameraWorldPos - _lastCameraPos) / _lastDeltaTime;
            _cameraVelocity = _cameraVelocity.Lerp(rawVel, 0.3f);
        }
        _lastCameraPos = cameraWorldPos;

        var cameraChunk = ChunkCoord.FromWorldPos(cameraWorldPos.X, cameraWorldPos.Z, WorldConstants.ChunkSize);
        bool chunkChanged = cameraChunk != _lastPlayerChunk;
        bool altChanged = MathF.Abs(cameraAltitude - _lastCameraAltitude) > 50f;

        if (chunkChanged || altChanged)
        {
            float speed = new Vector2(_cameraVelocity.X, _cameraVelocity.Z).Length();
            float cooldown = speed > HighSpeedThreshold ? HighSpeedCooldown : UpdateCooldown;

            _cooldownTimer += _lastDeltaTime;
            if (_cooldownTimer < cooldown)
                return;

            _cooldownTimer = 0f;
            _lastCameraAltitude = cameraAltitude;
            UpdateLoadedChunks(cameraChunk, cameraAltitude);
            _lastPlayerChunk = cameraChunk;
        }
        else
        {
            _cooldownTimer = 0f;
        }
    }

    public void UpdatePlayerPosition(Vector3 playerWorldPos)
    {
        UpdateCameraPosition(playerWorldPos, playerWorldPos.Y);
    }

    public void Update(float deltaTime)
    {
        _lastDeltaTime = deltaTime;

        while (_pendingQueue.Count > 0 && _activeWorkers < MaxWorkers)
        {
            var (coord, lodStep) = _pendingQueue.Dequeue();
            if (_chunks.ContainsKey(coord) || _generating.Contains(coord)) continue;
            _generating.Add(coord);
            Interlocked.Increment(ref _activeWorkers);
            var gen = _generator;
            var modLog = _modLog;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var data = gen.Generate(coord);
                    if (modLog.Count > 0)
                        HeightfieldGenerator.ApplyModifications(data, modLog, WorldConstants.ChunkSize);
                    data.LodLevel = lodStep;
                    _readyQueue.Enqueue(new GeneratedChunkResult(coord, data, lodStep));
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[ChunkManager] Background generation failed for {coord}: {ex.Message}");
                }
                finally
                {
                    Interlocked.Decrement(ref _activeWorkers);
                }
            });
        }

        int built = 0;
        while (_readyQueue.TryDequeue(out var result) && built < MaxVisualsPerFrame)
        {
            _generating.Remove(result.Coord);
            if (_chunks.ContainsKey(result.Coord)) continue;

            var loaded = new LoadedChunk { Data = result.Data, LodStep = result.LodStep };
            BuildChunkVisual(loaded);
            _chunks[result.Coord] = loaded;
            OnChunkLoaded?.Invoke(result.Data);
            built++;
        }

        bool forceUnload = _lastCameraAltitude > WorldConstants.SpaceAltitudeThreshold;
        int pendingNearby = 0;
        if (!forceUnload)
        {
            foreach (var (coord, _) in _pendingQueue)
            {
                if (coord.ChebyshevDistance(_lastPlayerChunk) <= 5)
                {
                    pendingNearby++;
                    break;
                }
            }
        }
        bool canUnload = forceUnload
                      || (pendingNearby == 0 && _generating.Count == 0)
                      || _unloadQueue.Count > MaxUnloadsPerFrame * 5;
        if (canUnload)
        {
            int maxUnloads = forceUnload ? MaxUnloadsPerFrame * 4 : MaxUnloadsPerFrame;
            int unloaded = 0;
            while (_unloadQueue.Count > 0 && unloaded < maxUnloads)
            {
                var coord = _unloadQueue.Dequeue();
                _generating.Remove(coord);
                if (_chunks.Remove(coord, out var removed))
                {
                    OnChunkUnloaded?.Invoke(coord);
                    if (removed.SceneNode != null)
                    {
                        if (removed.SceneNode.GetParent() != null)
                            removed.SceneNode.GetParent().RemoveChild(removed.SceneNode);
                        removed.SceneNode.Free();
                        removed.SceneNode = null;
                    }
                    unloaded++;
                }
            }
        }
    }

    private void UpdateLoadedChunks(ChunkCoord center, float cameraAltitude = 0f)
    {
        int loadR, highLodR;
        if (cameraAltitude > WorldConstants.SpaceAltitudeThreshold)
        {
            loadR = 0;
            highLodR = 0;
        }
        else if (cameraAltitude > WorldConstants.PlanetVisibleMaxAlt)
        {
            loadR = 2;
            highLodR = 0;
        }
        else if (cameraAltitude > 2000f)
        {
            loadR = 4;
            highLodR = 0;
        }
        else if (cameraAltitude > 500f)
        {
            loadR = 12;
            highLodR = 0;
        }
        else if (cameraAltitude > 100f)
        {
            loadR = 14;
            highLodR = 2;
        }
        else
        {
            loadR = 10;
            highLodR = WorldConstants.HighLodRadius;
        }

        loadR = Math.Min(loadR, MaxLoadRadius);
        int unloadR = loadR + 2;

        float speed = new System.Numerics.Vector2(_cameraVelocity.X, _cameraVelocity.Z).Length();
        bool highSpeed = speed > HighSpeedThreshold && cameraAltitude < WorldConstants.PlanetVisibleMaxAlt;

        ChunkCoord predictedCenter = center;
        float velDirX = 0f, velDirZ = 0f;
        if (highSpeed)
        {
            float predX = center.X * WorldConstants.ChunkSize + _cameraVelocity.X * PredictionLookahead;
            float predZ = center.Z * WorldConstants.ChunkSize + _cameraVelocity.Z * PredictionLookahead;
            predictedCenter = ChunkCoord.FromWorldPos(predX, predZ, WorldConstants.ChunkSize);
            float invSpeed = 1f / speed;
            velDirX = _cameraVelocity.X * invSpeed;
            velDirZ = _cameraVelocity.Z * invSpeed;
        }

        _pendingQueue.Clear();
        _unloadQueue.Clear();

        foreach (var coord in _chunks.Keys)
        {
            bool inRange = coord.ChebyshevDistance(center) <= unloadR;
            bool inPredRange = highSpeed && coord.ChebyshevDistance(predictedCenter) <= loadR;
            if (!inRange && !inPredRange && !IsInAnchoredRegion(coord))
                _unloadQueue.Enqueue(coord);
        }

        var needed = new HashSet<ChunkCoord>();
        AddSquareRange(needed, center, loadR);
        if (highSpeed)
            AddSquareRange(needed, predictedCenter, loadR);
        foreach (var (anchorCenter, anchorRadius) in _anchoredRegions)
            AddSquareRange(needed, anchorCenter, anchorRadius);

        var toLoad = new List<(ChunkCoord coord, float priority)>();
        foreach (var coord in needed)
        {
            if (_chunks.ContainsKey(coord) || _generating.Contains(coord)) continue;
            int dist = coord.ChebyshevDistance(center);
            float priority = dist;
            if (highSpeed && dist > 0)
            {
                float relX = coord.X - center.X;
                float relZ = coord.Z - center.Z;
                float invD = 1f / MathF.Sqrt(relX * relX + relZ * relZ);
                float dot = (relX * velDirX + relZ * velDirZ) * invD;
                priority -= dot * loadR * 0.5f;
            }
            toLoad.Add((coord, priority));
        }
        toLoad.Sort((a, b) => a.priority.CompareTo(b.priority));

        foreach (var (coord, _) in toLoad)
        {
            int dist = coord.ChebyshevDistance(center);
            int lodStep = dist <= highLodR ? 1 : dist <= loadR / 2 ? 2 : 4;
            _pendingQueue.Enqueue((coord, lodStep));
        }
    }

    private static void AddSquareRange(HashSet<ChunkCoord> set, ChunkCoord center, int radius)
    {
        for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
                set.Add(new ChunkCoord(center.X + dx, center.Z + dz));
    }

    private void InitialLoadChunks(ChunkCoord center)
    {
        int baseLoadR = WorldConstants.LoadRadius * 2;
        int highLodR = WorldConstants.HighLodRadius;

        var toLoad = new List<(ChunkCoord coord, int dist, int lodStep)>();
        for (int dz = -baseLoadR; dz <= baseLoadR; dz++)
        {
            for (int dx = -baseLoadR; dx <= baseLoadR; dx++)
            {
                var coord = new ChunkCoord(center.X + dx, center.Z + dz);
                if (_chunks.ContainsKey(coord)) continue;
                int dist = coord.ChebyshevDistance(center);
                int lodStep = dist <= highLodR ? 1 : dist <= baseLoadR / 2 ? 2 : 4;
                toLoad.Add((coord, dist, lodStep));
            }
        }
        toLoad.Sort((a, b) => a.dist.CompareTo(b.dist));

        var results = new ConcurrentBag<(ChunkCoord coord, HeightfieldChunk data, int lodStep, int dist)>();
        Parallel.ForEach(toLoad, new ParallelOptions { MaxDegreeOfParallelism = MaxWorkers }, item =>
        {
            var data = _generator.Generate(item.coord);
            if (_modLog.Count > 0)
                HeightfieldGenerator.ApplyModifications(data, _modLog, WorldConstants.ChunkSize);
            data.LodLevel = item.lodStep;
            results.Add((item.coord, data, item.lodStep, item.dist));
        });

        var sorted = results.OrderBy(r => r.dist).ToList();
        foreach (var (coord, data, lodStep, _) in sorted)
        {
            if (_chunks.ContainsKey(coord)) continue;
            var loaded = new LoadedChunk { Data = data, LodStep = lodStep };
            BuildChunkVisual(loaded);
            _chunks[coord] = loaded;
            OnChunkLoaded?.Invoke(data);
        }

        GD.Print($"[ChunkManager] Initial load: {sorted.Count} chunks (parallel, {MaxWorkers} workers)");
    }
}
