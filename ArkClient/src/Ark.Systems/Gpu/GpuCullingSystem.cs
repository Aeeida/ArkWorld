using System;
using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using Godot;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Gpu;

namespace Ark.Systems.Gpu;

/// <summary>
/// GPU 视锥剔除系统 — 使用 Compute Shader 批量剔除不可见实体。
/// 输出可见实体索引列表，用于 Indirect Draw 或 MultiMesh 筛选。
/// </summary>
public sealed class GpuCullingSystem
{
    [StructLayout(LayoutKind.Sequential)]
    private struct GpuBoundingData
    {
        public float MinX, MinY, MinZ, _pad0;
        public float MaxX, MaxY, MaxZ, EntityIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GpuVisibilityResult
    {
        public uint  Visible;
        public uint  LodLevel;
        public float DistanceSq;
        public uint  _pad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GpuCullingPushConstants
    {
        // 6 个视锥平面（每个 vec4）
        public float Plane0X, Plane0Y, Plane0Z, Plane0W;
        public float Plane1X, Plane1Y, Plane1Z, Plane1W;
        public float Plane2X, Plane2Y, Plane2Z, Plane2W;
        public float Plane3X, Plane3Y, Plane3Z, Plane3W;
        public float Plane4X, Plane4Y, Plane4Z, Plane4W;
        public float Plane5X, Plane5Y, Plane5Z, Plane5W;
        // 相机位置
        public float CamX, CamY, CamZ, _pad0;
        // 参数
        public uint  EntityCount;
        public float LodDist0, LodDist1, LodDist2;
    }

    private readonly GpuComputeManager _gpu;
    private readonly EntityStore       _store;

    private ComputePipeline? _pipeline;
    private GpuBuffer?       _boundingBuffer;
    private GpuBuffer?       _visibilityBuffer;
    private GpuBuffer?       _indirectCountBuffer;
    private GpuBuffer?       _visibleIndicesBuffer;
    private Rid              _uniformSet;

    private const int MaxEntities = 50000;

    // 上一帧的可见数量（用于 CPU 侧参考）
    public uint LastVisibleCount { get; private set; }

    /// <summary>GPU 系统是否成功初始化</summary>
    public bool IsInitialized => _pipeline != null && _boundingBuffer != null;

    public GpuCullingSystem(GpuComputeManager gpu, EntityStore store)
    {
        _gpu   = gpu;
        _store = store;
    }

    public void Initialize()
    {
        if (!_gpu.IsReady) return;

        _pipeline = _gpu.LoadPipeline("FrustumCulling", "res://src/Ark.Gpu/shaders/culling/frustum_culling.glsl");

        _boundingBuffer       = _gpu.CreateBuffer("CullingBounds", (uint)(MaxEntities * 32));
        _visibilityBuffer     = _gpu.CreateBuffer("CullingVisibility", (uint)(MaxEntities * 16));
        _indirectCountBuffer  = _gpu.CreateBuffer("CullingIndirectCount", 4);
        _visibleIndicesBuffer = _gpu.CreateBuffer("CullingVisibleIndices", (uint)(MaxEntities * 4));

        _uniformSet = _gpu.CreateUniformSet(_pipeline, 0,
            (0, _boundingBuffer),
            (1, _visibilityBuffer),
            (2, _indirectCountBuffer),
            (3, _visibleIndicesBuffer)
        );

        GD.Print("[GpuCulling] Initialized");
    }

    /// <summary>
    /// 从相机提取视锥平面并执行剔除。
    /// </summary>
    public void DispatchCulling(Camera3D camera, RenderingDevice rd)
    {
        if (_pipeline == null || _boundingBuffer == null) return;

        // ═══ 收集实体包围盒 ═══
        var query = _store.Query<WorldPosition, BoundingBox>().AllTags(Tags.Get<LodEnabled>());
        var boundingData = new GpuBoundingData[MaxEntities];
        int entityCount = 0;

        foreach (var chunk in query.Chunks)
        {
            var positions = chunk.Chunk1;
            var bounds = chunk.Chunk2;
            var entities = chunk.Entities;

            for (int i = 0; i < entities.Length && entityCount < MaxEntities; i++, entityCount++)
            {
                ref readonly var pos = ref positions.Span[i];
                ref readonly var bb  = ref bounds.Span[i];

                boundingData[entityCount] = new GpuBoundingData
                {
                    MinX = pos.X + bb.MinX,
                    MinY = pos.Y + bb.MinY,
                    MinZ = pos.Z + bb.MinZ,
                    MaxX = pos.X + bb.MaxX,
                    MaxY = pos.Y + bb.MaxY,
                    MaxZ = pos.Z + bb.MaxZ,
                    EntityIndex = entityCount
                };
            }
        }

        if (entityCount == 0) return;

        // 上传包围盒数据
        byte[] boundingBytes = MemoryMarshal.AsBytes(boundingData.AsSpan(0, entityCount)).ToArray();
        rd.BufferUpdate(_boundingBuffer.Rid, 0, (uint)boundingBytes.Length, boundingBytes);

        // 重置可见计数
        rd.BufferUpdate(_indirectCountBuffer.Rid, 0, 4, new byte[4]);

        // ═══ 提取视锥平面 ═══
        var frustum = camera.GetFrustum();
        var camPos  = camera.GlobalPosition;

        var pc = new GpuCullingPushConstants
        {
            EntityCount = (uint)entityCount,
            CamX = camPos.X, CamY = camPos.Y, CamZ = camPos.Z,
            LodDist0 = 50f,   // LOD0 → LOD1
            LodDist1 = 100f,  // LOD1 → LOD2
            LodDist2 = 200f   // LOD2 → 隐藏
        };

        // 填充视锥平面（Godot 返回 6 个 Plane）
        if (frustum.Count >= 6)
        {
            SetPlane(ref pc, 0, frustum[0]);
            SetPlane(ref pc, 1, frustum[1]);
            SetPlane(ref pc, 2, frustum[2]);
            SetPlane(ref pc, 3, frustum[3]);
            SetPlane(ref pc, 4, frustum[4]);
            SetPlane(ref pc, 5, frustum[5]);
        }

        byte[] pcBytes = PushConstantHelper.ToBytes(pc);

        // ═══ Dispatch ═══
        long computeList = rd.ComputeListBegin();
        rd.ComputeListBindComputePipeline(computeList, _pipeline.PipelineRid);
        rd.ComputeListBindUniformSet(computeList, _uniformSet, 0);
        rd.ComputeListSetPushConstant(computeList, pcBytes, (uint)pcBytes.Length);

        uint groupCount = GpuComputeManager.CalculateGroupCount((uint)entityCount, 256);
        rd.ComputeListDispatch(computeList, groupCount, 1, 1);
        rd.ComputeListEnd();
    }

    /// <summary>
    /// 读取可见实体数量（延迟 2-3 帧）。
    /// </summary>
    public void ReadVisibleCount(RenderingDevice rd)
    {
        if (_indirectCountBuffer == null) return;

        //rd.Barrier(RenderingDevice.BarrierMask.Compute);
        byte[] countData = rd.BufferGetData(_indirectCountBuffer.Rid, 0, 4);
        LastVisibleCount = BitConverter.ToUInt32(countData);
    }

    /// <summary>
    /// 获取可见实体索引 Buffer Rid（用于 Indirect Draw）。
    /// </summary>
    public Rid GetVisibleIndicesBufferRid() => _visibleIndicesBuffer?.Rid ?? default;

    private static void SetPlane(ref GpuCullingPushConstants pc, int index, Plane plane)
    {
        switch (index)
        {
            case 0: pc.Plane0X = plane.Normal.X; pc.Plane0Y = plane.Normal.Y; pc.Plane0Z = plane.Normal.Z; pc.Plane0W = plane.D; break;
            case 1: pc.Plane1X = plane.Normal.X; pc.Plane1Y = plane.Normal.Y; pc.Plane1Z = plane.Normal.Z; pc.Plane1W = plane.D; break;
            case 2: pc.Plane2X = plane.Normal.X; pc.Plane2Y = plane.Normal.Y; pc.Plane2Z = plane.Normal.Z; pc.Plane2W = plane.D; break;
            case 3: pc.Plane3X = plane.Normal.X; pc.Plane3Y = plane.Normal.Y; pc.Plane3Z = plane.Normal.Z; pc.Plane3W = plane.D; break;
            case 4: pc.Plane4X = plane.Normal.X; pc.Plane4Y = plane.Normal.Y; pc.Plane4Z = plane.Normal.Z; pc.Plane4W = plane.D; break;
            case 5: pc.Plane5X = plane.Normal.X; pc.Plane5Y = plane.Normal.Y; pc.Plane5Z = plane.Normal.Z; pc.Plane5W = plane.D; break;
        }
    }
}
