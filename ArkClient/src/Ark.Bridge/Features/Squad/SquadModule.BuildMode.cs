using Godot;

namespace Ark.Bridge.Features.Squad;

public partial class SquadModule
{
    public void ToggleBuildMode()
    {
        SetBuildMode(!_buildModeActive);
    }

    public void SetBuildMode(bool active)
    {
        if (_buildModeActive == active) return;

        _buildModeActive = active;

        _leaderController?.SetBuildCameraMode(active);

        OnBuildModeChanged?.Invoke(active);
        GD.Print($"[SquadModule] Build mode: {active}");
    }

    public Camera3D? GetActiveCamera()
    {
        return _leaderController?.Camera;
    }
}
