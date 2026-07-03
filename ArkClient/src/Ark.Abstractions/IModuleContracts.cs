using System.Text.Json.Nodes;

namespace Ark.Abstractions;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║               核心模块抽象 — 统一生命周期、持久化、动作执行                         ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// 可序列化对象 — 所有需要存档/读档的模块与实体应实现此接口。
/// </summary>
public interface ISavable
{
    JsonObject Save();
    void Load(JsonObject data);
}

/// <summary>
/// 游戏模块接口 — 所有功能模块的统一注册入口。
/// 支持插件式扩展：新增模块只需实现此接口并注册到 ModuleManager。
/// </summary>
public interface IModule
{
    /// <summary>模块唯一标识。</summary>
    string ModuleId { get; }

    /// <summary>初始化模块（依赖注入阶段）。</summary>
    void Initialize();

    /// <summary>每帧更新（由引擎驱动）。</summary>
    void Update(float deltaTime);

    /// <summary>关机清理。</summary>
    void Shutdown();
}

/// <summary>
/// 游戏动作接口 — 统一的动作定义与执行协议。
/// 人物日常动作（吃饭/睡觉/工作）和火箭操作（组装/发射）均通过此接口定义。
/// </summary>
public interface IGameAction
{
    /// <summary>动作唯一标识。</summary>
    string ActionId { get; }

    /// <summary>执行持续时间（秒），0 表示瞬时。</summary>
    float Duration { get; }

    /// <summary>检查是否满足执行前提条件。</summary>
    bool CanExecute(int entityId);

    /// <summary>开始执行动作。</summary>
    void Execute(int entityId);

    /// <summary>动作完成回调。</summary>
    void OnComplete(int entityId);
}

/// <summary>
/// 时间服务接口 — 统一时间管理，支持 KSP 式时间加速。
/// 人物日常用 1x 实时，火箭飞行可切换到 1000x+。
/// </summary>
public interface ITimeService
{
    /// <summary>当前游戏世界时间（秒）。</summary>
    float CurrentTime { get; }

    /// <summary>当前时间缩放（1x = 实时）。</summary>
    float TimeScale { get; }

    /// <summary>是否处于时间加速状态。</summary>
    bool IsWarping { get; }

    /// <summary>设置时间缩放。</summary>
    void SetTimeScale(float scale);

    /// <summary>推进时间。</summary>
    void AdvanceTime(float delta);
}
