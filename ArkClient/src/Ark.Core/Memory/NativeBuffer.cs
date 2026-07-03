using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ark.Core.Memory;

/// <summary>
/// 高性能原生缓冲区 — 用于 ECS ↔ GPU 零拷贝数据传输。
/// 支持 Span 访问，避免托管堆分配。
/// </summary>
public sealed class NativeBuffer<T> : IDisposable where T : unmanaged
{
    private T[]  _managed;
    private int  _count;
    private bool _disposed;

    public int Capacity { get; }
    public int Count => _count;

    public NativeBuffer(int capacity)
    {
        Capacity = capacity;
        _managed = GC.AllocateUninitializedArray<T>(capacity, pinned: true);
        _count   = 0;
    }

    /// <summary>获取可写 Span（用于并行填充）</summary>
    public Span<T> AsSpan() => _managed.AsSpan(0, _count);

    /// <summary>获取只读 Span</summary>
    public ReadOnlySpan<T> AsReadOnlySpan() => _managed.AsSpan(0, _count);

    /// <summary>转为 byte[]（用于 GPU 上传）— 零拷贝视图</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> AsByteSpan()
    {
        return MemoryMarshal.AsBytes(_managed.AsSpan(0, _count));
    }

    /// <summary>设置有效元素数量</summary>
    public void SetCount(int count)
    {
        if (count > Capacity)
            throw new ArgumentOutOfRangeException(nameof(count));
        _count = count;
    }

    /// <summary>重置计数（不清除数据）</summary>
    public void Clear() => _count = 0;

    /// <summary>直接索引访问</summary>
    public ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _managed[index];
    }

    /// <summary>批量拷贝到目标 Span</summary>
    public void CopyTo(Span<T> destination)
    {
        _managed.AsSpan(0, _count).CopyTo(destination);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _managed  = null!;
    }
}

/// <summary>
/// 双缓冲 — 用于 CPU 写入 / GPU 读取 的无锁切换。
/// </summary>
public sealed class DoubleBuffer<T> : IDisposable where T : unmanaged
{
    private readonly NativeBuffer<T> _front;
    private readonly NativeBuffer<T> _back;
    private volatile int _writeIndex; // 0 = front, 1 = back

    public DoubleBuffer(int capacity)
    {
        _front = new NativeBuffer<T>(capacity);
        _back  = new NativeBuffer<T>(capacity);
    }

    /// <summary>获取当前写入缓冲区（CPU 侧填充）</summary>
    public NativeBuffer<T> GetWriteBuffer() => _writeIndex == 0 ? _front : _back;

    /// <summary>获取当前读取缓冲区（GPU 侧消费）</summary>
    public NativeBuffer<T> GetReadBuffer() => _writeIndex == 0 ? _back : _front;

    /// <summary>交换缓冲区（帧末调用）</summary>
    public void Swap() => _writeIndex = 1 - _writeIndex;

    public void Dispose()
    {
        _front.Dispose();
        _back.Dispose();
    }
}
