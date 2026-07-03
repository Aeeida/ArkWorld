using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Godot;

namespace Ark.Gpu;

/// <summary>
/// GPU Compute 资源管理器 — 统一管理 Shader、Pipeline、Buffer 生命周期。
/// 所有 ComputeList 操作必须通过 RenderingServer.CallOnRenderThread 执行。
/// </summary>
public sealed class GpuComputeManager : IDisposable
{
    private RenderingDevice _rd = null!;
    private readonly Dictionary<string, ComputePipeline> _pipelines = new();
    private readonly Dictionary<string, GpuBuffer>       _buffers   = new();
    private readonly List<Rid> _allRids = new(); // 用于统一清理
    private bool _initialized;
    private bool _disposed;

    /// <summary>RenderingDevice 是否可用（主线程获取后才能用）</summary>
    public bool IsReady => _initialized && _rd != null;

    /// <summary>
    /// 初始化（必须在主线程调用一次）。
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;
        _rd = RenderingServer.GetRenderingDevice();
        if (_rd == null)
        {
            GD.PrintErr("[GpuCompute] RenderingDevice not available (Compatibility renderer?)");
            return;
        }
        _initialized = true;
        GD.Print("[GpuCompute] Initialized");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //                              Pipeline 管理
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 加载并编译 Compute Shader。
    /// </summary>
    public ComputePipeline LoadPipeline(string name, string shaderPath)
    {
        if (_pipelines.TryGetValue(name, out var existing))
            return existing;

        var shaderFile = GD.Load<RDShaderFile>(shaderPath);
        if (shaderFile == null)
            throw new FileNotFoundException($"Shader not found: {shaderPath}");

        var spirv    = shaderFile.GetSpirV();
        var shaderRid = _rd.ShaderCreateFromSpirV(spirv);
        var pipeline  = _rd.ComputePipelineCreate(shaderRid);

        _allRids.Add(shaderRid);
        _allRids.Add(pipeline);

        var result = new ComputePipeline
        {
            Name       = name,
            ShaderRid  = shaderRid,
            PipelineRid = pipeline,
            LocalSizeX = 256, // 默认，可从 SPIRV 解析
            LocalSizeY = 1,
            LocalSizeZ = 1
        };

        _pipelines[name] = result;
        GD.Print($"[GpuCompute] Pipeline '{name}' loaded");
        return result;
    }

    /// <summary>
    /// 获取已加载的 Pipeline。
    /// </summary>
    public ComputePipeline GetPipeline(string name)
    {
        return _pipelines.TryGetValue(name, out var p) ? p : throw new KeyNotFoundException(name);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //                              Buffer 管理
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 创建 StorageBuffer。
    /// </summary>
    public GpuBuffer CreateBuffer(string name, uint sizeBytes, RenderingDevice.StorageBufferUsage usage = 0)
    {
        if (_buffers.TryGetValue(name, out var existing))
        {
            if (existing.SizeBytes >= sizeBytes)
                return existing;
            // 需要更大的 buffer，释放旧的
            _rd.FreeRid(existing.Rid);
            _allRids.Remove(existing.Rid);
        }

        var rid = _rd.StorageBufferCreate(sizeBytes, null, usage);
        _allRids.Add(rid);

        var buffer = new GpuBuffer
        {
            Name      = name,
            Rid       = rid,
            SizeBytes = sizeBytes
        };

        _buffers[name] = buffer;
        return buffer;
    }

    /// <summary>
    /// 创建双缓冲（用于 CPU 写 / GPU 读 的无锁切换）。
    /// </summary>
    public GpuDoubleBuffer CreateDoubleBuffer(string name, uint sizeBytes)
    {
        var front = CreateBuffer($"{name}_front", sizeBytes);
        var back  = CreateBuffer($"{name}_back", sizeBytes);
        return new GpuDoubleBuffer(front, back);
    }

    /// <summary>
    /// 获取已创建的 Buffer。
    /// </summary>
    public GpuBuffer GetBuffer(string name)
    {
        return _buffers.TryGetValue(name, out var b) ? b : throw new KeyNotFoundException(name);
    }

    /// <summary>
    /// 上传数据到 Buffer（在渲染线程调用）。
    /// </summary>
    public void UploadBuffer(GpuBuffer buffer, ReadOnlySpan<byte> data)
    {
        _rd.BufferUpdate(buffer.Rid, 0, (uint)data.Length, data.ToArray());
    }

    /// <summary>
    /// 从 Buffer 读取数据（会阻塞等待 GPU，谨慎使用）。
    /// </summary>
    public byte[] DownloadBuffer(GpuBuffer buffer)
    {
        return _rd.BufferGetData(buffer.Rid);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //                              UniformSet 创建
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 创建 UniformSet（绑定多个 Buffer 到 Shader）。
    /// </summary>
    public Rid CreateUniformSet(ComputePipeline pipeline, uint setIndex, params (uint binding, GpuBuffer buffer)[] bindings)
    {
        var uniforms = new Godot.Collections.Array<RDUniform>();

        foreach (var (binding, buffer) in bindings)
        {
            var uniform = new RDUniform();
            uniform.UniformType = RenderingDevice.UniformType.StorageBuffer;
            uniform.Binding     = (int)binding;
            uniform.AddId(buffer.Rid);
            uniforms.Add(uniform);
        }

        var uniformSet = _rd.UniformSetCreate(uniforms, pipeline.ShaderRid, setIndex);
        _allRids.Add(uniformSet);
        return uniformSet;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //                              Dispatch 辅助
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 计算所需的工作组数量。
    /// </summary>
    public static uint CalculateGroupCount(uint totalItems, uint localSize)
    {
        return (totalItems + localSize - 1) / localSize;
    }

    /// <summary>
    /// 提交 Compute 命令（不阻塞）。
    /// </summary>
    public void Submit()
    {
        _rd.Submit();
    }

    /// <summary>
    /// 等待 GPU 完成（会阻塞！仅在必要时调用）。
    /// </summary>
    public void Sync()
    {
        _rd.Sync();
    }

    /// <summary>
    /// 设置 Compute Barrier（确保前一个 Compute 完成再执行后续）。
    /// </summary>
    public void Barrier()
    {
        _rd.Barrier(RenderingDevice.BarrierMask.Compute);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //                              生命周期
    // ═══════════════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        if (_disposed || _rd == null) return;
        _disposed = true;

        foreach (var rid in _allRids)
        {
            if (rid.IsValid)
                _rd.FreeRid(rid);
        }
        _allRids.Clear();
        _pipelines.Clear();
        _buffers.Clear();

        GD.Print("[GpuCompute] Disposed");
    }
}

/// <summary>
/// Compute Pipeline 信息。
/// </summary>
public class ComputePipeline
{
    public string Name        { get; init; } = "";
    public Rid    ShaderRid   { get; init; }
    public Rid    PipelineRid { get; init; }
    public uint   LocalSizeX  { get; init; }
    public uint   LocalSizeY  { get; init; }
    public uint   LocalSizeZ  { get; init; }
}

/// <summary>
/// GPU StorageBuffer 信息。
/// </summary>
public class GpuBuffer
{
    public string Name      { get; init; } = "";
    public Rid    Rid       { get; init; }
    public uint   SizeBytes { get; init; }
}

/// <summary>
/// GPU 双缓冲（用于 CPU/GPU 无锁数据交换）。
/// </summary>
public class GpuDoubleBuffer
{
    private readonly GpuBuffer _front;
    private readonly GpuBuffer _back;
    private int _writeIndex; // 0 = write to front, 1 = write to back

    public GpuDoubleBuffer(GpuBuffer front, GpuBuffer back)
    {
        _front = front;
        _back  = back;
    }

    public GpuBuffer WriteBuffer => _writeIndex == 0 ? _front : _back;
    public GpuBuffer ReadBuffer  => _writeIndex == 0 ? _back : _front;

    public void Swap() => _writeIndex = 1 - _writeIndex;
}

/// <summary>
/// Push Constants 辅助 — 将结构体转为 byte[]。
/// </summary>
public static class PushConstantHelper
{
    public static byte[] ToBytes<T>(T value) where T : unmanaged
    {
        int    size  = Marshal.SizeOf<T>();
        byte[] bytes = new byte[size];
        var    handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(value, handle.AddrOfPinnedObject(), false);
        }
        finally
        {
            handle.Free();
        }
        return bytes;
    }
}
