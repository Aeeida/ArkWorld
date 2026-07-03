namespace Ark.Configuration;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                   游戏配置系统 — 数据驱动的平衡参数                              ║
// ║  替代硬编码 const，支持运行时热加载和 JSON 配置文件                              ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// 游戏配置根对象 — 所有游戏平衡参数的统一入口。
/// 通过 <see cref="Current"/> 全局访问当前配置。
/// </summary>
public sealed class GameConfig
{
    /// <summary>当前活跃配置（默认值可通过 JSON 或代码覆盖）。</summary>
    public static GameConfig Current { get; set; } = new();

    public PlayerConfig Player { get; set; } = new();
    public CombatConfig Combat { get; set; } = new();
    public VehicleConfig Vehicle { get; set; } = new();
    public BuildingConfig Building { get; set; } = new();
    public WorldConfig World { get; set; } = new();
    public LifeConfig Life { get; set; } = new();
    public SpaceConfig Space { get; set; } = new();
}

/// <summary>角色参数配置。</summary>
public sealed class PlayerConfig
{
    public float WalkSpeed { get; set; } = 5f;
    public float SprintSpeed { get; set; } = 8f;
    public float JumpForce { get; set; } = 8f;
    public float Gravity { get; set; } = 20f;
    public int MaxJumps { get; set; } = 2;
    public float MouseSensitivity { get; set; } = 0.003f;
}

/// <summary>战斗参数配置。</summary>
public sealed class CombatConfig
{
    public float HeadshotMultiplier { get; set; } = 2.5f;
    public float FriendlyFireMultiplier { get; set; } = 0f;
    public float RespawnDelay { get; set; } = 5f;
    public float DamageNumberDuration { get; set; } = 1.5f;
}

/// <summary>载具参数配置。</summary>
public sealed class VehicleConfig
{
    public float EnterExitRadius { get; set; } = 5f;
    public float ExitCooldown { get; set; } = 1f;
}

/// <summary>建造参数配置。</summary>
public sealed class BuildingConfig
{
    public float PlacementGridSize { get; set; } = 1f;
    public float MaxBuildDistance { get; set; } = 20f;
}

/// <summary>世界参数配置。</summary>
public sealed class WorldConfig
{
    public float DayLengthSeconds { get; set; } = 600f;
    public float DefaultTimeScale { get; set; } = 1f;
}

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                    日常养成（Life）& 太空（Space）配置                           ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>日常养成参数配置 — 需求衰减速率、动作时长等。</summary>
public sealed class LifeConfig
{
    // ── 需求衰减率（每秒）──
    public float HungerDecay { get; set; } = 0.5f;
    public float EnergyDecay { get; set; } = 0.3f;
    public float BladderDecay { get; set; } = 0.4f;
    public float HygieneDecay { get; set; } = 0.2f;
    public float FunDecay { get; set; } = 0.6f;
    public float SocialDecay { get; set; } = 0.25f;

    // ── 阈值 ──
    public float NeedCriticalThreshold { get; set; } = 30f;
    public float NeedMax { get; set; } = 100f;

    // ── 动作时长（秒）──
    public float EatDuration { get; set; } = 300f;
    public float SleepDuration { get; set; } = 1800f;
    public float WorkDuration { get; set; } = 3600f;
    public float SocialChatDuration { get; set; } = 600f;

    // ── AI 决策间隔 ──
    public float AiDecisionInterval { get; set; } = 5f;
}

/// <summary>太空飞行参数配置 — 轨道计算、物理步长等。</summary>
public sealed class SpaceConfig
{
    // ── 星球参数（默认=地球）──
    public float PlanetRadius { get; set; } = 6_371_000f;
    public float PlanetMu { get; set; } = 3.986e14f;           // 引力参数 GM (m³/s²)
    public float AtmosphereHeight { get; set; } = 100_000f;     // 卡门线 (m)

    // ── 物理 ──
    public float PhysicsTimeStep { get; set; } = 0.02f;
    public float MaxTimeWarp { get; set; } = 100_000f;

    // ── 入轨判定 ──
    public float StableOrbitEccentricity { get; set; } = 0.01f;
    public float MinOrbitAltitude { get; set; } = 150_000f;
}
