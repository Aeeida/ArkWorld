using System.Runtime.InteropServices;
using Godot;
using Ark.Gpu;
using Ark.World.Core;
using Ark.World.Data;

namespace Ark.World.Terrain;

/// <summary>
/// GPU 地形生成系统 — 使用 Compute Shader 加速高度图生成。
///
/// 流程：
///   1. 将群系参数上传到 GPU buffer
///   2. Dispatch compute shader 生成 65×65 高度图
///   3. 读回高度数据填充 HeightfieldChunk
///
/// 对比 CPU 生成（HeightfieldGenerator）：
///   CPU: 65×65 = 4225 次 FBM 采样，串行 → ~2-5ms/区块
///   GPU: 4225 线程并行 → ~0.05ms/区块，40-100× 加速
///
/// 当 GPU 不可用时自动回退到 CPU 生成。
/// </summary>
public sealed class TerrainComputeSystem : IDisposable
{
    private readonly GpuComputeManager _gpu;
    private ComputePipeline? _pipeline;
    private GpuBuffer? _heightBuffer;
    private GpuBuffer? _biomeBuffer;
    private GpuBuffer? _biomeParamsBuffer;
    private Rid _uniformSet;
    private bool _initialized;

    /// <summary>GPU 地形生成是否可用。</summary>
    public bool IsAvailable => _initialized && _pipeline != null;

    public TerrainComputeSystem(GpuComputeManager gpu)
    {
        _gpu = gpu;
    }

    /// <summary>
    /// 初始化 GPU 管线和 buffer。
    /// </summary>
    public void Initialize()
    {
        if (!_gpu.IsReady)
        {
            GD.Print("[TerrainCompute] GPU not available, will fallback to CPU");
            return;
        }

        try
        {
            // 加载 compute shader
            _pipeline = _gpu.LoadPipeline("terrain_heightmap",
                "res://src/Ark.Gpu/shaders/terrain/terrain_heightmap.glsl");

            // 创建 buffer
            uint resolution = (uint)WorldConstants.HeightmapResolution;
            uint sampleCount = resolution * resolution;

            _heightBuffer = _gpu.CreateBuffer("terrain_heights",
                sampleCount * sizeof(float));
            _biomeBuffer = _gpu.CreateBuffer("terrain_biomes",
                sampleCount * sizeof(uint));

            // 群系参数（最多 8 种）
            uint biomeParamSize = (uint)(8 * Marshal.SizeOf<GpuBiomeParams>());
            _biomeParamsBuffer = _gpu.CreateBuffer("terrain_biome_params", biomeParamSize);

            // 上传群系参数
            UploadBiomeParams();

            // 创建 UniformSet
            _uniformSet = _gpu.CreateUniformSet(_pipeline, 0,
                (0, _heightBuffer),
                (1, _biomeBuffer),
                (2, _biomeParamsBuffer));

            _initialized = true;
            GD.Print("[TerrainCompute] GPU terrain generation initialized");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[TerrainCompute] Init failed: {ex.Message}");
            _initialized = false;
        }
    }

    /// <summary>
    /// GPU 生成区块高度图。
    /// </summary>
    /// <param name="coord">区块坐标。</param>
    /// <param name="seed">世界种子。</param>
    /// <returns>填充好的 HeightfieldChunk，失败返回 null。</returns>
    public HeightfieldChunk? GenerateChunk(ChunkCoord coord, WorldSeed seed)
    {
        if (!IsAvailable || _pipeline == null || _heightBuffer == null || _biomeBuffer == null)
            return null;

        int resolution = WorldConstants.HeightmapResolution;
        var origin = coord.ToWorldOrigin(WorldConstants.ChunkSize);

        // Push constants
        var pushData = new TerrainPushConstants
        {
            ChunkOriginX = origin.X,
            ChunkOriginZ = origin.Z,
            ChunkSize = WorldConstants.ChunkSize,
            Resolution = (uint)resolution,
            MaxTerrainHeight = WorldConstants.MaxTerrainHeight,
            HeightSeed = (uint)(seed.Derive("height").Value & 0xFFFFFFFF),
            BiomeSeed = (uint)(seed.Derive("biome").Value & 0xFFFFFFFF),
            BiomeCount = (uint)BiomeRegistry.All.Count,
        };
        var pushBytes = PushConstantHelper.ToBytes(pushData);

        // Dispatch
        var rd = RenderingServer.GetRenderingDevice();
        uint totalSamples = (uint)(resolution * resolution);
        uint groupCount = GpuComputeManager.CalculateGroupCount(totalSamples, 256);

        var computeList = rd.ComputeListBegin();
        rd.ComputeListBindComputePipeline(computeList, _pipeline.PipelineRid);
        rd.ComputeListBindUniformSet(computeList, _uniformSet, 0);
        rd.ComputeListSetPushConstant(computeList, pushBytes, (uint)pushBytes.Length);
        rd.ComputeListDispatch(computeList, groupCount, 1, 1);
        rd.ComputeListEnd();

        // 提交并等待结果
        _gpu.Submit();
        _gpu.Sync();

        // 读回数据
        var heightBytes = _gpu.DownloadBuffer(_heightBuffer);
        var biomeBytes = _gpu.DownloadBuffer(_biomeBuffer);

        // 构建 HeightfieldChunk
        var chunk = new HeightfieldChunk(coord, resolution);
        Buffer.BlockCopy(heightBytes, 0, chunk.Heights, 0,
            Math.Min(heightBytes.Length, chunk.Heights.Length * sizeof(float)));

        // 群系 ID 映射
        var biomeIds = MemoryMarshal.Cast<byte, uint>(biomeBytes);
        var registeredBiomes = BiomeRegistry.All.ToArray();
        for (int i = 0; i < chunk.Biomes.Length && i < biomeIds.Length; i++)
        {
            int biomeIdx = (int)Math.Min(biomeIds[i], (uint)(registeredBiomes.Length - 1));
            chunk.Biomes[i] = registeredBiomes[biomeIdx].Id;
        }

        return chunk;
    }

    private void UploadBiomeParams()
    {
        if (_biomeParamsBuffer == null) return;

        var biomes = BiomeRegistry.All.ToArray();
        var paramArray = new GpuBiomeParams[8]; // 最多 8 群系

        for (int i = 0; i < Math.Min(biomes.Length, 8); i++)
        {
            paramArray[i] = new GpuBiomeParams
            {
                BaseHeight = biomes[i].BaseHeight,
                HeightAmplitude = biomes[i].HeightAmplitude,
                FrequencyScale = biomes[i].FrequencyScale,
                Octaves = biomes[i].Octaves,
                UseRidged = biomes[i].UseRidgedNoise ? 1f : 0f,
                SlopeThreshold = biomes[i].SlopeThresholdDeg,
            };
        }

        var bytes = new byte[8 * Marshal.SizeOf<GpuBiomeParams>()];
        var handle = GCHandle.Alloc(paramArray, GCHandleType.Pinned);
        try
        {
            Marshal.Copy(handle.AddrOfPinnedObject(), bytes, 0, bytes.Length);
        }
        finally
        {
            handle.Free();
        }

        _gpu.UploadBuffer(_biomeParamsBuffer, bytes);
    }

    public void Dispose()
    {
        _initialized = false;
        _pipeline = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          GPU 数据结构（与 GLSL 对齐）
    // ═══════════════════════════════════════════════════════════════════════

    [StructLayout(LayoutKind.Sequential)]
    private struct TerrainPushConstants
    {
        public float ChunkOriginX;
        public float ChunkOriginZ;
        public float ChunkSize;
        public uint Resolution;
        public float MaxTerrainHeight;
        public uint HeightSeed;
        public uint BiomeSeed;
        public uint BiomeCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GpuBiomeParams
    {
        public float BaseHeight;
        public float HeightAmplitude;
        public float FrequencyScale;
        public float Octaves;
        public float UseRidged;
        public float SlopeThreshold;
        public float _pad0;
        public float _pad1;
    }
}
