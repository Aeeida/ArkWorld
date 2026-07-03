using Friflo.Engine.ECS;
using Ark.Analyzers.Attributes;
using Ark.Ecs.Components;

namespace Ark.Bridge.Features.BaseBuilding;

/// <summary>
/// 非 Node 的 ECS 写授权：BuildingVisualManager 通过它将逐帧累积的视觉反馈
/// 状态（BuildingDamageFeedbackState 等）写回到 ECS 实体（满足 ARK005 / ECS005）。
/// </summary>
[EcsAuthorityBridge]
internal sealed class BuildingDamageEcsAuthority
{
    public void WriteFeedback(Entity entity, in BuildingDamageFeedbackState feedback)
    {
        if (entity.IsNull) return;
        entity.AddComponent(feedback);
    }
}
