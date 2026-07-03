using Godot;

namespace Ark.Abstractions;

/// <summary>
/// 相机模式 — 定义相机的行为方式。
/// </summary>
public enum CameraMode
{
    /// <summary>第三人称跟随（角色默认）</summary>
    ThirdPerson,

    /// <summary>第一人称（FPS 视角）</summary>
    FirstPerson,

    /// <summary>RTS 俯视（建造模式）</summary>
    TopDown,

    /// <summary>轨道环绕（观察物体）</summary>
    Orbit,

    /// <summary>自由相机（编辑器/观战）</summary>
    Free,

    /// <summary>锁定跟随（子弹/载具）</summary>
    Follow,

    /// <summary>固定位置（监控视角）</summary>
    Fixed,
}

/// <summary>
/// 相机目标接口 — 任何可以被相机跟随的对象都应实现此接口。
/// </summary>
public interface ICameraTarget
{
    /// <summary>相机锚点位置（世界坐标）</summary>
    Vector3 CameraAnchorPosition { get; }

    /// <summary>相机锚点朝向（用于确定前方）</summary>
    Quaternion CameraAnchorRotation { get; }

    /// <summary>推荐的相机模式</summary>
    CameraMode PreferredCameraMode { get; }

    /// <summary>默认相机偏移（相对于锚点）</summary>
    Vector3 DefaultCameraOffset { get; }

    /// <summary>是否允许玩家控制输入（移动/瞄准）</summary>
    bool CanReceiveInput { get; }

    /// <summary>是否允许相机旋转</summary>
    bool AllowCameraRotation { get; }

    /// <summary>最小缩放距离</summary>
    float MinZoom { get; }

    /// <summary>最大缩放距离</summary>
    float MaxZoom { get; }

    /// <summary>当相机附加到此目标时调用</summary>
    void OnCameraAttached();

    /// <summary>当相机从此目标分离时调用</summary>
    void OnCameraDetached();
}

/// <summary>
/// 可控制的相机目标 — 支持玩家输入控制的目标。
/// </summary>
public interface IControllableCameraTarget : ICameraTarget
{
    /// <summary>处理移动输入</summary>
    void ProcessMovementInput(Vector2 input, bool sprint);

    /// <summary>处理跳跃输入</summary>
    void ProcessJumpInput();

    /// <summary>处理瞄准输入（视角旋转）</summary>
    void ProcessAimInput(Vector2 delta);

    /// <summary>处理交互输入</summary>
    void ProcessInteractInput();

    /// <summary>处理主要动作输入（射击/使用）</summary>
    void ProcessPrimaryAction(bool pressed);

    /// <summary>处理次要动作输入（瞄准/格挡）</summary>
    void ProcessSecondaryAction(bool pressed);
}

/// <summary>
/// 相机目标优先级 — 用于自动切换相机目标。
/// </summary>
public enum CameraTargetPriority
{
    Lowest    = 0,
    Low       = 100,
    Normal    = 200,
    High      = 300,
    Highest   = 400,
    Cinematic = 500,
}

/// <summary>
/// 相机目标信息 — 用于注册和管理相机目标。
/// </summary>
public readonly struct CameraTargetInfo
{
    public readonly ICameraTarget Target;
    public readonly string Id;
    public readonly string DisplayName;
    public readonly CameraTargetPriority Priority;

    public CameraTargetInfo(ICameraTarget target, string id, string displayName,
        CameraTargetPriority priority = CameraTargetPriority.Normal)
    {
        Target      = target;
        Id          = id;
        DisplayName = displayName;
        Priority    = priority;
    }
}
