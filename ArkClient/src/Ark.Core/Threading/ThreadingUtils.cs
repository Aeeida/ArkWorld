using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Ark.Core.Threading;

/// <summary>
/// 无锁多生产者单消费者队列 — 用于网络线程 → 主线程、Worker → 主线程。
/// </summary>
public sealed class MpscQueue<T>
{
    private readonly ConcurrentQueue<T> _queue = new();

    public void Enqueue(T item) => _queue.Enqueue(item);

    public bool TryDequeue(out T item) => _queue.TryDequeue(out item!);

    /// <summary>批量消费（主线程调用）— 返回本次消费数量</summary>
    public int DrainTo(Span<T> buffer)
    {
        int count = 0;
        while (count < buffer.Length && _queue.TryDequeue(out var item))
        {
            buffer[count++] = item;
        }
        return count;
    }

    /// <summary>批量消费到 Action（适合不定长场景）</summary>
    public int DrainAll(Action<T> consumer, int maxPerFrame = int.MaxValue)
    {
        int count = 0;
        while (count < maxPerFrame && _queue.TryDequeue(out var item))
        {
            consumer(item);
            count++;
        }
        return count;
    }

    public int Count => _queue.Count;
    public bool IsEmpty => _queue.IsEmpty;
}

/// <summary>
/// 帧限流器 — 将重任务分摊到多帧执行。
/// </summary>
public sealed class FrameThrottler
{
    private readonly int _itemsPerFrame;
    private int _frameIndex;

    public FrameThrottler(int itemsPerFrame = 100)
    {
        _itemsPerFrame = itemsPerFrame;
    }

    /// <summary>
    /// 判断当前帧是否应该处理指定索引的实体。
    /// 用法：只有 ShouldProcess(entityIndex) 返回 true 才处理。
    /// </summary>
    public bool ShouldProcess(int entityIndex, int totalEntities)
    {
        if (totalEntities <= _itemsPerFrame) return true;

        int framesNeeded = (totalEntities + _itemsPerFrame - 1) / _itemsPerFrame;
        int frameSlot    = entityIndex / _itemsPerFrame;
        return (frameSlot % framesNeeded) == (_frameIndex % framesNeeded);
    }

    /// <summary>帧末调用</summary>
    public void AdvanceFrame() => _frameIndex++;
}

/// <summary>
/// 简单自旋锁 — 用于极短临界区（避免内核态切换）。
/// </summary>
public struct SpinLockSlim
{
    private volatile int _locked;

    public void Enter()
    {
        while (Interlocked.Exchange(ref _locked, 1) == 1)
        {
            Thread.SpinWait(10);
        }
    }

    public void Exit() => Volatile.Write(ref _locked, 0);
}
