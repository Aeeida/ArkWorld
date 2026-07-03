using Friflo.Engine.ECS;
using Ark.Analyzers.Attributes;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;

namespace Ark.Bridge.Features.Squad;

/// <summary>
/// 非 Node 的 ECS 结构变更授权门面：SquadModule 持有它并通过它对实体进行
/// AddComponent/RemoveTag/CreateEntity/DeleteEntity 等结构变更，使 Node 自身不再
/// 直接触碰 Friflo Entity 的结构变更 API（满足 ARK005 / ECS005 ECS-FIRST 边界）。
/// 当小队业务迁到服务端授权后，应移除本类上的 [EcsAuthorityBridge]。
/// </summary>
[EcsAuthorityBridge]
internal sealed class SquadEcsAuthority
{
    private readonly EntityStore _store;

    public SquadEcsAuthority(EntityStore store)
    {
        _store = store;
    }

    public EntityStore Store => _store;

    public void InitializeLeader(Entity leader)
    {
        leader.AddComponent(new SquadMember
        {
            SquadId = 0,
            SlotIndex = 0,
            IsControlled = 1,
            ColorIndex = 0
        });
        leader.AddTag<SquadLeader>();
        leader.AddTag<InSquad>();
        leader.AddTag<Controllable>();
    }

    public void DeactivateLeader(Entity leader, byte formationType)
    {
        if (leader.Id == 0) return;
        var sm = leader.GetComponent<SquadMember>();
        sm.IsControlled = 0;
        leader.AddComponent(sm);
        leader.RemoveTag<SquadLeader>();
        leader.AddTag<SquadFollower>();
        leader.AddTag<Following>();
        leader.AddComponent(new FormationOffset
        {
            OffsetX = 0f,
            OffsetZ = -2f,
            FormationType = formationType
        });
        leader.AddComponent(new AiMovement { HasArrived = 1, IsMoving = 0 });
    }

    public void DeactivateMember(Entity member, byte formationType, in SquadFormationOffset offset)
    {
        if (member.Id == 0) return;
        var sm = member.GetComponent<SquadMember>();
        sm.IsControlled = 0;
        member.AddComponent(sm);
        member.AddTag<Following>();
        member.RemoveTag<SquadLeader>();
        member.AddTag<SquadFollower>();
        member.AddComponent(new FormationOffset
        {
            OffsetX = offset.X,
            OffsetZ = offset.Z,
            FormationType = formationType
        });
        member.AddComponent(new AiMovement { HasArrived = 1, IsMoving = 0 });
    }

    public void ActivateLeader(Entity leader)
    {
        if (leader.Id == 0) return;
        var sm = leader.GetComponent<SquadMember>();
        sm.IsControlled = 1;
        leader.AddComponent(sm);
        leader.AddTag<SquadLeader>();
        leader.RemoveTag<SquadFollower>();
        leader.RemoveTag<Following>();
    }

    public void ActivateMember(Entity member)
    {
        if (member.Id == 0) return;
        var sm = member.GetComponent<SquadMember>();
        sm.IsControlled = 1;
        member.AddComponent(sm);
        member.RemoveTag<Following>();
        member.AddTag<SquadLeader>();
        member.RemoveTag<SquadFollower>();
    }

    public Entity SpawnMemberEntity(
        in System.Numerics.Vector3 position,
        int slotIndex,
        byte formationType,
        int leaderEntityId,
        in SquadFormationOffset formationOffset)
    {
        var entity = _store.CreateEntity();
        entity.AddComponent(new WorldPosition { X = position.X, Y = position.Y, Z = position.Z });
        entity.AddComponent(new WorldRotation { W = 1f });
        entity.AddComponent(new Velocity());
        entity.AddComponent(new Health { Current = 80, Max = 80 });
        entity.AddComponent(new MoveInput());
        entity.AddComponent(new AiMovement { HasArrived = 1 });
        entity.AddComponent(CombatTarget.None);

        entity.AddComponent(new SquadMember
        {
            SquadId = 0,
            SlotIndex = (byte)slotIndex,
            IsControlled = 0,
            ColorIndex = (byte)(slotIndex - 1)
        });
        entity.AddComponent(new FollowTarget
        {
            TargetEntityId = leaderEntityId,
            FollowDistance = 3f,
            StopDistance = 1.5f,
            AngleOffset = 0f
        });
        entity.AddComponent(new FormationOffset
        {
            OffsetX = formationOffset.X,
            OffsetZ = formationOffset.Z,
            FormationType = formationType
        });

        entity.AddTag<SquadFollower>();
        entity.AddTag<InSquad>();
        entity.AddTag<Controllable>();
        entity.AddTag<Following>();

        return entity;
    }

    public void DeleteEntity(Entity entity)
    {
        if (entity.Id == 0) return;
        entity.DeleteEntity();
    }

    public void UpdateMemberFormationOffset(Entity member, byte formationType, in SquadFormationOffset offset)
    {
        if (member.Id == 0) return;
        member.AddComponent(new FormationOffset
        {
            OffsetX = offset.X,
            OffsetZ = offset.Z,
            FormationType = formationType
        });
    }
}

/// <summary>
/// 阵型偏移的轻量值类型，避免 Authority 和 SquadModule 之间互依特定 X/Z 容器类型。
/// </summary>
internal readonly record struct SquadFormationOffset(float X, float Z);
