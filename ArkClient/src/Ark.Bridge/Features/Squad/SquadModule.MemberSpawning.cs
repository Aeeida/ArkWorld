using Godot;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Abstractions;

namespace Ark.Bridge.Features.Squad;

public partial class SquadModule
{
    public void SpawnAllMembers()
    {
        if (_store == null || _leaderController == null || MemberFactory == null) return;

        var leaderPos = _leaderController.GlobalPosition;
        var formation = SquadFormations.Get(_formationType);

        for (int i = 1; i <= MaxMembers; i++)
        {
            var offset = formation[i];
            var spawnPos = leaderPos + new Vector3(offset.X, 0, offset.Z);
            SpawnMember(i, spawnPos);
        }

        GD.Print($"[SquadModule] Spawned {MaxMembers} squad members");
    }

    public ISquadMemberController? SpawnMember(int slotIndex, Vector3 position)
    {
        if (_store == null || _squadAuth == null || slotIndex < 1 || slotIndex > MaxMembers || MemberFactory == null)
            return null;

        if (slotIndex <= _members.Count && _members[slotIndex - 1] != null)
            RemoveMember(slotIndex);

        var raw = SquadFormations.Get(_formationType)[slotIndex];
        var entity = _squadAuth.SpawnMemberEntity(
            new System.Numerics.Vector3(position.X, position.Y, position.Z),
            slotIndex,
            _formationType,
            _leaderEntity.Id,
            new SquadFormationOffset(raw.X, raw.Z));

        var color = MemberColors[slotIndex - 1];
        var controller = MemberFactory(_store, entity, slotIndex, color);
        controller.GlobalPosition = position;

        if (controller is Node node)
            AddChild(node);

        while (_members.Count < slotIndex)
        {
            _members.Add(null);
            _memberEntities.Add(default);
        }
        _members[slotIndex - 1] = controller;
        _memberEntities[slotIndex - 1] = entity;

        return controller;
    }

    public void RemoveMember(int slotIndex)
    {
        if (slotIndex < 1 || slotIndex > _members.Count) return;

        var controller = _members[slotIndex - 1];
        var entity = _memberEntities[slotIndex - 1];

        controller?.QueueFree();
        _squadAuth?.DeleteEntity(entity);

        _members[slotIndex - 1] = null;
        _memberEntities[slotIndex - 1] = default;
    }
}
