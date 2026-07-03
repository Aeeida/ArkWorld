using Godot;
using Friflo.Engine.ECS;

namespace Ark.Abstractions;

/// <summary>
/// 可控实体的输入状态接口 — 角色、载具等均应实现。
/// </summary>
public interface IControllable
{
    /// <summary>鼠标是否处于锁定（FPS/TPS 模式）。</summary>
    bool IsMouseCaptured { get; }

    /// <summary>是否处于建造模式。</summary>
    bool IsBuildModeActive { get; }

    /// <summary>当前活动相机。</summary>
    Camera3D? Camera { get; }

    /// <summary>是否处于载具中。</summary>
    bool InVehicle { get; }

    /// <summary>当前载具实体 ID（-1 表示不在载具中）。</summary>
    int VehicleEntityId { get; }

    /// <summary>循环切换到下一个载具座位。</summary>
    void CycleSeat();
}

/// <summary>
/// 小队成员控制器接口 — 用于解耦 Ark.Bridge 与 Ark.Player。
/// </summary>
public interface ISquadMemberController : ICameraTarget, IControllable
{
    /// <summary>关联的 ECS 实体</summary>
    Entity Entity { get; }

    /// <summary>小队槽位索引 (1-5)</summary>
    int SlotIndex { get; }

    /// <summary>是否由玩家控制</summary>
    bool IsControlled { get; set; }

    /// <summary>世界位置</summary>
    Vector3 GlobalPosition { get; set; }

    /// <summary>相机（如果已附加）</summary>
    new Camera3D? Camera { get; }

    /// <summary>设置移动目标</summary>
    void SetMoveTarget(Vector3 target, float speed);

    /// <summary>停止移动</summary>
    void Stop();

    /// <summary>传送到指定位置</summary>
    void TeleportTo(Vector3 position);

    /// <summary>销毁节点</summary>
    void QueueFree();

    /// <summary>附加相机到此控制器</summary>
    void AttachCamera(Camera3D? camera);

    /// <summary>分离相机</summary>
    void DetachCamera();

    /// <summary>设置建造相机模式（俯视）</summary>
    void SetBuildCameraMode(bool active);

    /// <summary>
    /// 注入战斗模块（由 GameBootstrap 调用）。
    /// 参数为 object 以避免 Ark.Shared 依赖 Ark.Gameplay.Combat。
    /// </summary>
    void SetCombatModule(object? combatModule);

    /// <summary>
    /// 注入地形高度查询回调（由 GameBootstrap 调用）。
    /// </summary>
    void SetTerrainQuery(Func<float, float, float>? sampleHeight);
}

/// <summary>
/// 玩家控制器接口 — 用于解耦 Ark.Bridge 与 Ark.Player。
/// </summary>
public interface IPlayerController : ICameraTarget, IControllable
{
    /// <summary>玩家相机</summary>
    new Camera3D? Camera { get; }

    /// <summary>世界位置</summary>
    Vector3 GlobalPosition { get; }

    /// <summary>关联的 ECS 实体</summary>
    Entity Entity { get; }

    /// <summary>是否处于激活状态</summary>
    bool IsActive { get; }

    /// <summary>鼠标是否处于锁定（FPS/TPS 模式）状态。</summary>
    new bool IsMouseCaptured { get; }

    /// <summary>是否处于建造模式。</summary>
    new bool IsBuildModeActive { get; }

    /// <summary>设置 ECS 引用</summary>
    void SetEntity(EntityStore store, Entity entity);

    /// <summary>设置建造模式（已废弃，改用 SetBuildCameraMode）</summary>
    void SetBuildModeActive(bool active);

    /// <summary>启用/禁用相机</summary>
    void SetCameraActive(bool active);

    /// <summary>设置建造相机模式（俯视）</summary>
    void SetBuildCameraMode(bool active);
}
