namespace Ark.Events;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                         游戏事件标记接口与通用事件                                ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// 标记接口 — 所有游戏事件应实现此接口（可选，用于约束泛型）。
/// </summary>
public interface IGameEvent;

/// <summary>实体生成事件。</summary>
public readonly record struct EntitySpawnedEvent(int EntityId, string EntityType) : IGameEvent;

/// <summary>实体销毁事件。</summary>
public readonly record struct EntityDestroyedEvent(int EntityId) : IGameEvent;

/// <summary>模块初始化完成事件。</summary>
public readonly record struct ModuleReadyEvent(string ModuleName) : IGameEvent;

/// <summary>游戏状态变更事件。</summary>
public readonly record struct GameStateChangedEvent(string PreviousState, string NewState) : IGameEvent;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                         领域事件 — 模块间通信                                    ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>建筑放置事件。</summary>
public readonly record struct BuildingPlacedEvent(
    int EntityId, float PosX, float PosY, float PosZ,
    float RotX, float RotY, float RotZ, float RotW,
    int TypeId) : IGameEvent;

/// <summary>建筑摧毁事件。</summary>
public readonly record struct BuildingDestroyedEvent(int EntityId) : IGameEvent;

/// <summary>结构坍塌事件。</summary>
public readonly record struct StructureCollapsedEvent(int EntityId) : IGameEvent;

/// <summary>武器开火事件。</summary>
public readonly record struct WeaponFiredEvent(int EntityId, int WeaponDefId) : IGameEvent;

/// <summary>投射物命中事件。</summary>
public readonly record struct ProjectileHitEvent(
    int TargetEntityId, float PosX, float PosY, float PosZ,
    float Damage, byte DamageType) : IGameEvent;

/// <summary>爆炸事件。</summary>
public readonly record struct ExplosionEvent(
    float PosX, float PosY, float PosZ, float Radius) : IGameEvent;

/// <summary>载具生成请求事件。</summary>
public readonly record struct VehicleSpawnRequestEvent(
    int VehicleDefId, float PosX, float PosY, float PosZ) : IGameEvent;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                         玩法模式切换                                           ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>玩法模式。</summary>
public enum GameplayMode : byte
{
    /// <summary>日常养成（Sims 风格）</summary>
    Life,
    /// <summary>战斗（TPS 射击 + 载具）</summary>
    Combat,
    /// <summary>太空飞行（KSP 风格）</summary>
    Space
}

/// <summary>玩法模式切换事件。</summary>
public readonly record struct GameplayModeChangedEvent(
    GameplayMode PreviousMode, GameplayMode NewMode) : IGameEvent;
