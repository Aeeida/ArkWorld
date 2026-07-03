using System;
using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using Godot;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Gpu;

namespace Ark.Systems.Gpu;

/// <summary>
/// GPU 碰撞系统 — 使用 Compute Shader 进行大规模实体与静态碰撞体的碰撞检测。
///
/// 工作流程：
/// 1. 收集所有建筑物 AABB（由 CPU 维护，增量更新）
/// 2. 上传到 GPU 碰撞体缓冲区
/// 3. 在移动 Shader 中采样碰撞体，推离/避让
///
/// 适用于：
/// - 5000+ GPU 模拟的 NPC/怪物
/// - 静态建筑障碍物（AABB 近似）
/// </summary>
public sealed class GpuCollisionSystem
{
    // ═══════════════════════════════════════════════════════════════════════
    //                          数据结构
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GPU 侧碰撞体数据（AABB）— 必须与 GLSL 布局一致。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuCollider
    {
        public float MinX, MinY, MinZ, _pad0;
        public float MaxX, MaxY, MaxZ, _pad1;
    }

    /// <summary>
    /// Push Constants — 碰撞检测参数。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct GpuCollisionPushConstants
    {
        public uint  ColliderCount;
        public float PushStrength;    // 推离力度
        public float SkinWidth;       // 碰撞皮肤宽度
        public uint  _pad;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          字段
    // ═══════════════════════════════════════════════════════════════════════

    private readonly GpuComputeManager _gpu;
    private readonly EntityStore       _store;

    private GpuBuffer?  _colliderBuffer;
    private GpuCollider[] _cpuColliders;
    private int         _colliderCount;
    private bool        _dirty;

    private const int MaxColliders    = 2000;
    private const int ColliderSize    = 32; // sizeof(GpuCollider)

    /// <summary>GPU 碰撞系统是否可用</summary>
    public bool IsReady => _colliderBuffer != null && _gpu.IsReady;

    /// <summary>当前碰撞体数量</summary>
    public int ColliderCount => _colliderCount;

    /// <summary>碰撞体缓冲区 Rid（供移动 Shader 绑定）</summary>
    public Rid ColliderBufferRid => _colliderBuffer?.Rid ?? default;

    public GpuCollisionSystem(GpuComputeManager gpu, EntityStore store)
    {
        _gpu   = gpu;
        _store = store;
        _cpuColliders = new GpuCollider[MaxColliders];
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          初始化
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 初始化 GPU 缓冲区。
    /// </summary>
    public void Initialize()
    {
        if (!_gpu.IsReady)
        {
            GD.PrintErr("[GpuCollision] GPU not ready");
            return;
        }

        _colliderBuffer = _gpu.CreateBuffer("CollisionAABBs", (uint)(MaxColliders * ColliderSize));
        if (_colliderBuffer == null)
        {
            GD.PrintErr("[GpuCollision] Failed to create collider buffer");
            return;
        }

        GD.Print("[GpuCollision] Initialized");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          碰撞体管理
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 添加碰撞体（通常在建筑物放置时调用）。
    /// </summary>
    public int AddCollider(Vector3 min, Vector3 max)
    {
        if (_colliderCount >= MaxColliders)
        {
            GD.PrintErr("[GpuCollision] Max colliders reached");
            return -1;
        }

        int index = _colliderCount++;
        _cpuColliders[index] = new GpuCollider
        {
            MinX = min.X, MinY = min.Y, MinZ = min.Z,
            MaxX = max.X, MaxY = max.Y, MaxZ = max.Z
        };
        _dirty = true;
        return index;
    }

    /// <summary>
    /// 从建筑物定义添加碰撞体。
    /// </summary>
    public int AddBuildingCollider(Vector3 worldPos, Vector3 size)
    {
        var halfSize = size * 0.5f;
        var min = new Vector3(worldPos.X - halfSize.X, worldPos.Y, worldPos.Z - halfSize.Z);
        var max = new Vector3(worldPos.X + halfSize.X, worldPos.Y + size.Y, worldPos.Z + halfSize.Z);
        return AddCollider(min, max);
    }

    /// <summary>
    /// 移除碰撞体（通过交换删除，保持紧凑）。
    /// </summary>
    public void RemoveCollider(int index)
    {
        if (index < 0 || index >= _colliderCount) return;

        // 用最后一个覆盖
        _colliderCount--;
        if (index < _colliderCount)
        {
            _cpuColliders[index] = _cpuColliders[_colliderCount];
        }
        _dirty = true;
    }

    /// <summary>
    /// 清空所有碰撞体。
    /// </summary>
    public void ClearColliders()
    {
        _colliderCount = 0;
        _dirty = true;
    }

    /// <summary>
    /// 从 ECS 同步所有建筑物碰撞体。
    /// </summary>
    public void SyncFromEcs()
    {
        _colliderCount = 0;

        var query = _store.Query<WorldPosition, BoundingBox>().AllTags(Tags.Get<BuildingTag>());

        foreach (var chunk in query.Chunks)
        {
            var positions = chunk.Chunk1;
            var bounds    = chunk.Chunk2;

            for (int i = 0; i < chunk.Entities.Length && _colliderCount < MaxColliders; i++)
            {
                ref readonly var pos = ref positions.Span[i];
                ref readonly var bb  = ref bounds.Span[i];

                _cpuColliders[_colliderCount++] = new GpuCollider
                {
                    MinX = pos.X + bb.MinX,
                    MinY = pos.Y + bb.MinY,
                    MinZ = pos.Z + bb.MinZ,
                    MaxX = pos.X + bb.MaxX,
                    MaxY = pos.Y + bb.MaxY,
                    MaxZ = pos.Z + bb.MaxZ
                };
            }
        }

        _dirty = true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          GPU 上传
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 如果数据有变更，上传到 GPU。
    /// </summary>
    public void UploadIfDirty(RenderingDevice rd)
    {
        if (!_dirty || _colliderBuffer == null || _colliderCount == 0) return;

        var bytes = MemoryMarshal.AsBytes(_cpuColliders.AsSpan(0, _colliderCount)).ToArray();
        rd.BufferUpdate(_colliderBuffer.Rid, 0, (uint)bytes.Length, bytes);

        _dirty = false;
    }

    /// <summary>
    /// 强制上传（无论是否 dirty）。
    /// </summary>
    public void ForceUpload(RenderingDevice rd)
    {
        _dirty = true;
        UploadIfDirty(rd);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          CPU 碰撞检测（备用）
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// CPU 侧 AABB 碰撞检测（用于少量角色，如玩家小队）。
    /// </summary>
    public bool CheckCollision(Vector3 position, float radius, out Vector3 pushDirection)
    {
        pushDirection = Vector3.Zero;

        float closestDistSq = float.MaxValue;

        for (int i = 0; i < _colliderCount; i++)
        {
            ref readonly var c = ref _cpuColliders[i];

            // 找到 AABB 上最近点
            float closestX = Math.Clamp(position.X, c.MinX, c.MaxX);
            float closestY = Math.Clamp(position.Y, c.MinY, c.MaxY);
            float closestZ = Math.Clamp(position.Z, c.MinZ, c.MaxZ);

            float dx = position.X - closestX;
            float dy = position.Y - closestY;
            float dz = position.Z - closestZ;
            float distSq = dx * dx + dy * dy + dz * dz;

            if (distSq < radius * radius && distSq < closestDistSq)
            {
                closestDistSq = distSq;

                if (distSq > 0.0001f)
                {
                    float dist = MathF.Sqrt(distSq);
                    pushDirection = new Vector3(dx / dist, dy / dist, dz / dist);
                }
                else
                {
                    // 在 AABB 内部，找最近面推出
                    float toMinX = position.X - c.MinX;
                    float toMaxX = c.MaxX - position.X;
                    float toMinZ = position.Z - c.MinZ;
                    float toMaxZ = c.MaxZ - position.Z;

                    float minDist = Math.Min(Math.Min(toMinX, toMaxX), Math.Min(toMinZ, toMaxZ));

                    if (minDist == toMinX)      pushDirection = new Vector3(-1, 0, 0);
                    else if (minDist == toMaxX) pushDirection = new Vector3( 1, 0, 0);
                    else if (minDist == toMinZ) pushDirection = new Vector3(0, 0, -1);
                    else                        pushDirection = new Vector3(0, 0,  1);
                }
            }
        }

        return closestDistSq < radius * radius;
    }

    /// <summary>
    /// 推离碰撞体（用于 CharacterBody3D 的补充检测）。
    /// </summary>
    public Vector3 ResolveCollision(Vector3 position, float radius, float pushStrength = 1f)
    {
        if (CheckCollision(position, radius, out var pushDir))
        {
            return position + pushDir * pushStrength;
        }
        return position;
    }
}
