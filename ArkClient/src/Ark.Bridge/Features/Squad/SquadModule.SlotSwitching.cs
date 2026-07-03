using Godot;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Abstractions;

namespace Ark.Bridge.Features.Squad;

public partial class SquadModule
{
    public override void _Input(InputEvent @event)
    {
        if (Input.IsActionJustPressed("build_mode"))
        {
            ToggleBuildMode();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (Input.IsActionJustPressed("squad_select_leader"))
        {
            SwitchToSlot(0);
            GetViewport().SetInputAsHandled();
            return;
        }

        for (int i = 1; i <= MaxMembers; i++)
        {
            if (Input.IsActionJustPressed($"squad_select_{i}"))
            {
                SwitchToSlot(i);
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (Input.IsActionJustPressed("squad_formation"))
        {
            var active = ActiveControllable;
            if (active != null && active.InVehicle)
                active.CycleSeat();
            else
                CycleFormation();
            GetViewport().SetInputAsHandled();
        }
    }

    public void SwitchToSlot(int slotIndex)
    {
        if (slotIndex == _activeSlot) return;
        if (slotIndex < 0 || slotIndex > _members.Count) return;
        if (slotIndex > 0 && _members[slotIndex - 1] == null) return;

        if (_buildModeActive)
            SetBuildMode(false);

        int previousSlot = _activeSlot;
        DeactivateCurrentSlot();
        _activeSlot = slotIndex;
        ActivateCurrentSlot();
        RebindLeaderController();
        RefreshMemberControllerPresentation();

        OnLeaderChanged?.Invoke(ActiveEntity);
        GD.Print($"[SquadModule] Switched from slot {previousSlot} to slot {slotIndex}");
        OnActiveChanged?.Invoke(slotIndex);
    }

    private void DeactivateCurrentSlot()
    {
        if (_squadAuth == null) return;
        var formation = SquadFormations.Get(_formationType);

        if (_activeSlot == 0)
        {
            _squadAuth.DeactivateLeader(_leaderEntity, _formationType);
        }
        else if (_activeSlot <= _members.Count && _members[_activeSlot - 1] != null)
        {
            var entity = _memberEntities[_activeSlot - 1];
            var raw = formation[_activeSlot];
            _squadAuth.DeactivateMember(entity, _formationType, new SquadFormationOffset(raw.X, raw.Z));
        }
    }

    private void ActivateCurrentSlot()
    {
        if (_squadAuth == null) return;

        if (_activeSlot == 0)
        {
            _squadAuth.ActivateLeader(_leaderEntity);
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        else if (_activeSlot <= _members.Count && _members[_activeSlot - 1] != null)
        {
            _squadAuth.ActivateMember(_memberEntities[_activeSlot - 1]);
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    private void RebindLeaderController()
    {
        if (_store == null || _leaderController == null)
            return;

        var activeEntity = ActiveEntity;
        if (activeEntity.Id == 0)
            return;

        _leaderController.SetEntity(_store, activeEntity);
    }

    private void RefreshMemberControllerPresentation()
    {
        for (int i = 0; i < _members.Count; i++)
        {
            var controller = _members[i];
            if (controller is null)
                continue;

            bool isActiveMember = _activeSlot == i + 1;
            controller.IsControlled = false;

            if (controller is Node memberNode)
                memberNode.ProcessMode = isActiveMember ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
            if (controller is Node3D memberNode3D)
                memberNode3D.Visible = !isActiveMember;

            if (!isActiveMember && _memberEntities[i].Id != 0 && _memberEntities[i].TryGetComponent<WorldPosition>(out var pos))
                controller.TeleportTo(new Vector3(pos.X, pos.Y, pos.Z));
        }
    }
}
