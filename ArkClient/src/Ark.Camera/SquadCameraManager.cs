using Godot;
using Ark.Bridge.Features.Squad;
using Ark.Abstractions;

namespace Ark.Camera;

/// <summary>
/// 小队相机管理器 — 处理角色切换时的相机 reparent 和建造模式相机过渡。
///
/// 职责：
///   • F1-F6 切换角色时，从旧角色分离相机并附加到新角色
///   • 建造模式切换时，更新 BuildPlacement 相机引用
///
/// 不持有相机实例 — 通过 IPlayerController / ISquadMemberController 间接访问。
/// </summary>
public sealed class SquadCameraManager
{
    private readonly SquadModule _squad;
    private readonly IPlayerController _player;

    /// <summary>相机切换完成后回调（新相机）— 供 BuildPlacement 更新引用。</summary>
    public System.Action<Camera3D?>? OnCameraChanged;

    public SquadCameraManager(SquadModule squad, IPlayerController player)
    {
        _squad  = squad;
        _player = player;
    }

    /// <summary>
    /// 处理角色切换事件。
    /// </summary>
    public void OnActiveChanged(int slotIndex)
    {
        if (_player.Camera == null) return;

        var camera = _player.Camera;
        var playerNode = _player as Node3D;
        var cameraArm = playerNode?.GetNodeOrNull<Node3D>("CameraRig/CameraArm");

        if (camera != null && camera.GetParent() != cameraArm && cameraArm != null)
        {
            camera.GetParent()?.RemoveChild(camera);
            cameraArm.AddChild(camera);
            camera.Position = new Vector3(0, 0.5f, 4f);
        }

        _player.SetCameraActive(true);
        OnCameraChanged?.Invoke(_player.Camera);
    }

    /// <summary>
    /// 处理建造模式切换事件。
    /// </summary>
    public void OnBuildModeChanged(bool active)
    {
        var camera = _squad.GetActiveCamera();
        OnCameraChanged?.Invoke(camera);
    }
}
