using System;
using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using Godot;
using Ark.Core.Memory;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Gpu;

namespace Ark.Systems.Gpu;

/// <summary>
/// GPU 群体移动系统 — 使用 Compute Shader 并行计算数千实体的移动。
/// 输出直接写入 MultiMesh buffer，零 CPU 回读。
/// </summary>
public sealed class GpuMovementSystem
{
    // GPU 侧数据布局（必须与 GLSL 完全一致）
    [StructLayout(LayoutKind.Sequential)]
    private struct GpuEntityData
    {
        public float PosX, PosY, PosZ, _pad0;
        public float VelX, VelY, VelZ, Speed;
        public float InputX, InputY, InputZ, TargetSpeed;
        public float RotX, RotY, RotZ, RotW;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GpuPushConstants
    {
        public float DeltaTime;
        public uint  EntityCount;
        public float MaxSpeed;
        public float Acceleration;
    }

    private readonly GpuComputeManager _gpu;
    private readonly EntityStore       _store;

    private ComputePipeline?     _pipeline;
    private GpuBuffer?           _entityBuffer;
    private GpuBuffer?           _transformBuffer;
    private Rid                  _uniformSet;

    private NativeBuffer<GpuEntityData>? _cpuBuffer;

    private const int MaxEntities     = 50000;
    private const int EntityDataSize  = 64; // sizeof(GpuEntityData)
    private const int TransformSize   = 48; // 3 * vec4 for MultiMesh

    /// <summary>GPU 系统是否成功初始化</summary>
    public bool IsInitialized => _pipeline != null && _entityBuffer != null;

    public GpuMovementSystem(GpuComputeManager gpu, EntityStore store)
    {
        _gpu   = gpu;
        _store = store;
    }

    /// <summary>
    /// 初始化 GPU 资源（必须在主线程调用）。
    /// </summary>
    public void Initialize()
    {
        if (!_gpu.IsReady)
        {
            GD.PrintErr("[GpuMovement] GPU not ready, cannot initialize");
            return;
        }

        try
        {
            // 加载 Compute Shader
            _pipeline = _gpu.LoadPipeline("CrowdMovement", "res://src/Ark.Gpu/shaders/movement/crowd_movement.glsl");
            if (_pipeline == null)
            {
                GD.PrintErr("[GpuMovement] Failed to load pipeline");
                return;
            }

            // 创建 GPU Buffer
            _entityBuffer    = _gpu.CreateBuffer("MovementEntities", (uint)(MaxEntities * EntityDataSize));
            _transformBuffer = _gpu.CreateBuffer("MovementTransforms", (uint)(MaxEntities * TransformSize));

            if (_entityBuffer == null || _transformBuffer == null)
            {
                GD.PrintErr("[GpuMovement] Failed to create buffers");
                return;
            }

            // 创建 UniformSet
            _uniformSet = _gpu.CreateUniformSet(_pipeline, 0,
                (0, _entityBuffer),
                (1, _transformBuffer)
            );

            // CPU 侧缓冲区（用于打包 ECS 数据）
            _cpuBuffer = new NativeBuffer<GpuEntityData>(MaxEntities);

            GD.Print($"[GpuMovement] Initialized successfully (Pipeline: {_pipeline.PipelineRid.Id != 0}, UniformSet: {_uniformSet.Id != 0})");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GpuMovement] Initialization failed: {ex.Message}");
            _pipeline = null;
        }
    }

    /// <summary>
    /// 每帧更新：打包 ECS 数据 → 上传 GPU → Dispatch。
    /// 必须通过 CallOnRenderThread 执行 Dispatch 部分。
    /// </summary>
    public (byte[] entityBytes, int count, float deltaTime) PrepareDispatch(float deltaTime)
    {
        if (_cpuBuffer == null)
            return (Array.Empty<byte>(), 0, deltaTime);

        // 查询带 GpuSimulated 标签的实体
        var query = _store.Query<WorldPosition, Velocity, MoveInput, WorldRotation>()
            .AllTags(Tags.Get<GpuSimulated>());

        // ═══ 在主线程打包 ECS 数据到 CPU 缓冲区 ═══
        int index = 0;
        foreach (var chunk in query.Chunks)
        {
            var positions = chunk.Chunk1;
            var velocities = chunk.Chunk2;
            var inputs = chunk.Chunk3;
            var rotations = chunk.Chunk4;
            var entities = chunk.Entities;

            for (int i = 0; i < entities.Length && index < MaxEntities; i++, index++)
            {
                ref var gpu = ref _cpuBuffer[index];
                ref readonly var pos = ref positions.Span[i];
                ref readonly var vel = ref velocities.Span[i];
                ref readonly var inp = ref inputs.Span[i];
                ref readonly var rot = ref rotations.Span[i];

                gpu.PosX = pos.X; gpu.PosY = pos.Y; gpu.PosZ = pos.Z;
                gpu.VelX = vel.X; gpu.VelY = vel.Y; gpu.VelZ = vel.Z; gpu.Speed = vel.Speed;
                gpu.InputX = inp.DirX; gpu.InputY = inp.DirY; gpu.InputZ = inp.DirZ;
                gpu.TargetSpeed = inp.TargetSpeed;
                gpu.RotX = rot.X; gpu.RotY = rot.Y; gpu.RotZ = rot.Z; gpu.RotW = rot.W;
            }
        }

        _cpuBuffer.SetCount(index);
        return (_cpuBuffer.AsByteSpan().ToArray(), index, deltaTime);
    }

    /// <summary>
    /// 在渲染线程执行 GPU Dispatch（由 GpuComputeNode 调用）。
    /// </summary>
    public void DispatchOnRenderThread(byte[] entityBytes, int entityCount, float deltaTime, RenderingDevice rd)
    {
        if (!IsInitialized || entityCount == 0) return;
        if (_pipeline == null || _entityBuffer == null || _uniformSet.Id == 0) return;

        // 上传数据到 GPU
        rd.BufferUpdate(_entityBuffer.Rid, 0, (uint)entityBytes.Length, entityBytes);

        // Push Constants
        var pc = new GpuPushConstants
        {
            DeltaTime    = deltaTime,
            EntityCount  = (uint)entityCount,
            MaxSpeed     = 10f,
            Acceleration = 5f
        };
        byte[] pcBytes = PushConstantHelper.ToBytes(pc);

        // ═══ Dispatch Compute ═══
        long computeList = rd.ComputeListBegin();
        rd.ComputeListBindComputePipeline(computeList, _pipeline.PipelineRid);
        rd.ComputeListBindUniformSet(computeList, _uniformSet, 0);
        rd.ComputeListSetPushConstant(computeList, pcBytes, (uint)pcBytes.Length);

        uint groupCount = GpuComputeManager.CalculateGroupCount((uint)entityCount, 256);
        rd.ComputeListDispatch(computeList, groupCount, 1, 1);
        rd.ComputeListEnd();
    }

    /// <summary>
    /// 获取 Transform Buffer Rid（供 MultiMesh 直接绑定）。
    /// </summary>
    public Rid GetTransformBufferRid() => _transformBuffer?.Rid ?? default;

    /// <summary>
    /// 将 GPU 结果回写到 ECS（可选，仅当 CPU 需要最新位置时）。
    /// 应在 Dispatch 后 2-3 帧调用以避免 GPU 阻塞。
    /// </summary>
    public void SyncResultsToEcs(RenderingDevice rd)
    {
        if (_entityBuffer == null) return;

        // 注意：Godot 4.x 中 Barrier 已自动处理，不再需要手动调用
        // rd.Barrier(RenderingDevice.BarrierMask.Compute); // Deprecated
        byte[] gpuData = rd.BufferGetData(_entityBuffer.Rid);

        // 解析并写回 ECS
        var gpuSpan = MemoryMarshal.Cast<byte, GpuEntityData>(gpuData);

        var query = _store.Query<WorldPosition, Velocity>().AllTags(Tags.Get<GpuSimulated>());
        int index = 0;

        foreach (var chunk in query.Chunks)
        {
            var positions = chunk.Chunk1;
            var velocities = chunk.Chunk2;

            for (int i = 0; i < chunk.Entities.Length && index < gpuSpan.Length; i++, index++)
            {
                ref readonly var gpu = ref gpuSpan[index];
                ref var pos = ref positions.Span[i];
                ref var vel = ref velocities.Span[i];

                pos.X = gpu.PosX; pos.Y = gpu.PosY; pos.Z = gpu.PosZ;
                vel.X = gpu.VelX; vel.Y = gpu.VelY; vel.Z = gpu.VelZ; vel.Speed = gpu.Speed;
            }
        }
    }

    public void Cleanup()
    {
        _cpuBuffer?.Dispose();
    }
}
