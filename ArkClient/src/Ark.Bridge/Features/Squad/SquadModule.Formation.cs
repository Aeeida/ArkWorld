using Godot;
using Ark.Ecs.Components;

namespace Ark.Bridge.Features.Squad;

public partial class SquadModule
{
    public void CycleFormation()
    {
        _formationType = (byte)((_formationType + 1) % 4);
        UpdateFormationOffsets();
        GD.Print($"[SquadModule] Formation changed to {_formationType}");
        OnFormationChanged?.Invoke(_formationType);
    }

    public void SetFormation(byte formationType)
    {
        if (formationType == _formationType) return;
        _formationType = (byte)(formationType % 4);
        UpdateFormationOffsets();
        OnFormationChanged?.Invoke(_formationType);
    }

    private void UpdateFormationOffsets()
    {
        if (_squadAuth == null) return;
        var formation = SquadFormations.Get(_formationType);

        for (int i = 0; i < _members.Count; i++)
        {
            if (_memberEntities[i].Id != 0)
            {
                int slot = i + 1;
                var raw = formation[slot];
                _squadAuth.UpdateMemberFormationOffset(_memberEntities[i], _formationType, new SquadFormationOffset(raw.X, raw.Z));
            }
        }
    }
}
