using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Ark.Ecs.Bridge;
using Ark.Core.Memory;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;

namespace Ark.Systems.Sync;

/// <summary>
/// MultiMesh 批渲染同步系统 — 将 ECS Transform 批量写入 MultiMesh Buffer。
/// ⚠️ 必须在主线程执行（访问 RenderingServer）。
/// 
/// 关键优化：使用 MultimeshSetBuffer 一次性写入，避免逐实例 SetInstanceTransform 调用。
/// </summary>
public sealed class MultiMeshSyncSystem
{
    // MultiMesh 组配置
    public struct MultiMeshGroup
    {
        public int                   GroupId;
        public MultiMeshInstance3D   Instance;
        public int                   MaxInstances;
        public NativeBuffer<float>   TransformBuffer; // 每实例 12 float
    }

    private readonly EntityStore _store;
    private readonly MultiMeshGroup[] _groups;
    private int _groupCount;

    // Transform buffer 布局：每实例 12 float（3x4 矩阵，列主序）
    private const int FloatsPerInstance = 12;

    public MultiMeshSyncSystem(EntityStore store, int maxGroups = 10)
    {
        _store  = store;
        _groups = new MultiMeshGroup[maxGroups];
    }

    /// <summary>
    /// 注册一个 MultiMesh 组（例如：怪物组、NPC 组、环境组）。
    /// </summary>
    public void RegisterGroup(int groupId, MultiMeshInstance3D instance, int maxInstances)
    {
        if (_groupCount >= _groups.Length)
        {
            GD.PrintErr("[MultiMeshSync] Too many groups");
            return;
        }

        // 配置 MultiMesh（必须在设置 InstanceCount 之前配置格式）
        var mm = instance.Multimesh;

        // 确保 InstanceCount 为 0，以便可以修改格式
        mm.InstanceCount = 0;

        // 配置格式选项
        mm.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
        mm.UseColors = false;
        mm.UseCustomData = false;

        // 设置自定义 AABB（防止被错误剔除）
        mm.CustomAabb = new Aabb(
            new Vector3(-1000, -100, -1000),
            new Vector3(2000, 200, 2000)
        );

        // 注意：不在这里设置 InstanceCount，SyncToMultiMesh 会动态设置

        _groups[_groupCount++] = new MultiMeshGroup
        {
            GroupId         = groupId,
            Instance        = instance,
            MaxInstances    = maxInstances,
            TransformBuffer = new NativeBuffer<float>(maxInstances * FloatsPerInstance)
        };

        GD.Print($"[MultiMeshSync] Registered group {groupId} with max {maxInstances} instances");
    }

    /// <summary>
    /// 每帧更新：收集 ECS Transform → 批量写入 MultiMesh。
    /// </summary>
    public void SyncToMultiMesh()
    {
        for (int g = 0; g < _groupCount; g++)
        {
            ref var group = ref _groups[g];
            var buffer = group.TransformBuffer;
            buffer.Clear();

            int instanceIndex = 0;

            // 查询属于该组的实体
            var query = _store.Query<WorldPosition, WorldRotation, MultiMeshSlot>()
                .AllTags(Tags.Get<UsesMultiMesh>());

            foreach (var chunk in query.Chunks)
            {
                var positions = chunk.Chunk1;
                var rotations = chunk.Chunk2;
                var slots = chunk.Chunk3;
                var entities = chunk.Entities;

                for (int i = 0; i < entities.Length; i++)
                {
                    // 只处理属于当前组的实体
                    if (slots.Span[i].MeshGroupId != group.GroupId)
                        continue;

                    if (instanceIndex >= group.MaxInstances)
                        break;

                    ref readonly var pos = ref positions.Span[i];
                    ref readonly var rot = ref rotations.Span[i];

                    // 四元数转旋转矩阵（列主序 3x4）
                    QuaternionToMatrix(
                        rot.X, rot.Y, rot.Z, rot.W,
                        pos.X, pos.Y, pos.Z,
                        buffer, instanceIndex * FloatsPerInstance
                    );

                    // 更新槽位索引（供其他系统参考）
                    slots.Span[i].InstanceIndex = instanceIndex;

                    instanceIndex++;
                }
            }

            // ═══ 关键：动态调整实例数以匹配缓冲区 ═══
            var mm = group.Instance.Multimesh;

            if (instanceIndex > 0)
            {
                // 先设置 InstanceCount 为 0，这样可以重新配置
                // 然后设置为实际的实例数量
                if (mm.InstanceCount != instanceIndex)
                {
                    mm.InstanceCount = instanceIndex;
                }

                buffer.SetCount(instanceIndex * FloatsPerInstance);

                // 获取完整缓冲区并写入
                var floatSpan = buffer.AsSpan();
                var floatArray = floatSpan.ToArray();

                // 使用 MultimeshSetBuffer 一次性写入
                RenderingServer.MultimeshSetBuffer(mm.GetRid(), floatArray);

                // 设置所有实例可见
                mm.VisibleInstanceCount = instanceIndex;

                // Debug: 首次输出
                if (group.GroupId == 1 && _debugCounter++ < 3)
                {
                    GD.Print($"[MultiMeshSync] Group {group.GroupId}: {instanceIndex} instances, buffer size: {floatArray.Length}");
                }
            }
            else
            {
                mm.VisibleInstanceCount = 0;
            }
        }
    }

    private int _debugCounter = 0;

    /// <summary>
    /// 四元数转 3x4 变换矩阵（列主序，直接写入 float buffer）。
    /// </summary>
    private static void QuaternionToMatrix(
        float qx, float qy, float qz, float qw,
        float px, float py, float pz,
        NativeBuffer<float> buffer, int offset)
    {
        float xx = qx * qx, yy = qy * qy, zz = qz * qz;
        float xy = qx * qy, xz = qx * qz, yz = qy * qz;
        float wx = qw * qx, wy = qw * qy, wz = qw * qz;

        // Column 0 (X axis)
        buffer[offset + 0] = 1f - 2f * (yy + zz);
        buffer[offset + 1] = 2f * (xy + wz);
        buffer[offset + 2] = 2f * (xz - wy);

        // Column 1 (Y axis)
        buffer[offset + 3] = 2f * (xy - wz);
        buffer[offset + 4] = 1f - 2f * (xx + zz);
        buffer[offset + 5] = 2f * (yz + wx);

        // Column 2 (Z axis)
        buffer[offset + 6] = 2f * (xz + wy);
        buffer[offset + 7] = 2f * (yz - wx);
        buffer[offset + 8] = 1f - 2f * (xx + yy);

        // Column 3 (Origin)
        buffer[offset + 9]  = px;
        buffer[offset + 10] = py;
        buffer[offset + 11] = pz;
    }

    public void Cleanup()
    {
        for (int i = 0; i < _groupCount; i++)
        {
            _groups[i].TransformBuffer?.Dispose();
        }
    }
}

/// <summary>
/// Node3D Transform 同步系统 — 将 ECS Transform 同步到带 NodeRef 的实体。
/// 仅用于近景/可交互实体（远景用 MultiMesh）。
/// ⚠️ 必须在主线程执行。
/// </summary>
public sealed class NodeTransformSyncSystem : QuerySystem<WorldPosition, WorldRotation, NodeRef>
{
    protected override void OnUpdate()
    {
        // ⚠️ 必须 Sequential — 访问 Godot Node
        foreach (var chunk in Query.Chunks)
        {
            var positions = chunk.Chunk1;
            var rotations = chunk.Chunk2;
            var nodeRefs = chunk.Chunk3;

            for (int i = 0; i < chunk.Entities.Length; i++)
            {
                ref readonly var pos = ref positions.Span[i];
                ref readonly var rot = ref rotations.Span[i];
                ref var nodeRef = ref nodeRefs.Span[i];

                var node = nodeRef.Get();
                if (node == null) continue;

                // 直接设置 Transform（避免分开设置 position/rotation 产生两次通知）
                var transform = node.Transform;
                transform.Origin = new Vector3(pos.X, pos.Y, pos.Z);
                transform.Basis = new Basis(new Quaternion(rot.X, rot.Y, rot.Z, rot.W));
                node.Transform = transform;
            }
        }
    }
}

/// <summary>
/// RenderingServer 直驱同步系统 — 用于不需要 Node 但需要渲染的实体。
/// 比 Node3D 开销更低，但不支持动画等复杂功能。
/// </summary>
public sealed class DirectRenderSyncSystem : QuerySystem<WorldPosition, WorldRotation, RidRef>
{
    protected override void OnUpdate()
    {
        foreach (var chunk in Query.Chunks)
        {
            var positions = chunk.Chunk1;
            var rotations = chunk.Chunk2;
            var ridRefs = chunk.Chunk3;

            for (int i = 0; i < chunk.Entities.Length; i++)
            {
                ref readonly var pos = ref positions.Span[i];
                ref readonly var rot = ref rotations.Span[i];
                ref readonly var ridRef = ref ridRefs.Span[i];

                if (!ridRef.IsValid) continue;

                var transform = new Transform3D(
                    new Basis(new Quaternion(rot.X, rot.Y, rot.Z, rot.W)),
                    new Vector3(pos.X, pos.Y, pos.Z)
                );

                RenderingServer.InstanceSetTransform(ridRef.GetRid(), transform);
            }
        }
    }
}
