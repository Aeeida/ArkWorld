using System;
using System.Numerics;
using System.Threading.Tasks;
using Ark.Shared.Data;

namespace Ark.Abstractions;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                     高层服务接口 — 游戏逻辑的唯一入口                            ║
// ║  所有游戏功能通过这些接口访问，底层实现可切换（Local Demo / Remote Server）        ║
// ║                                                                              ║
// ║  ⚠️ 此库使用 System.Numerics 而非 Godot 类型，确保跨项目兼容性                   ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// 游戏世界服务 — 当前单一路径下的最小世界访问表面。
/// </summary>
public interface IGameWorld
{
    /// <summary>当前世界是否已加载</summary>
    bool IsLoaded { get; }

    /// <summary>本地玩家 Entity ID</summary>
    int LocalPlayerId { get; }

    void DestroyEntity(int entityId);
    float GetWorldTime();
}

/// <summary>
/// 网络服务 — 当前单一路径下的最小网络入口。
/// </summary>
public interface INetworkService
{
    Task<bool> ConnectAsync(string address, int port);
    void SendPlayerInput(PlayerInputData input);
}

/// <summary>
/// 战斗服务。
/// </summary>
public interface ICombatService
{
    WeaponInfo? CurrentWeapon { get; }
    int AimTargetId { get; }

    void Attack(Vector3 aimDirection);
    void Reload();
    void SwitchWeapon(int weaponSlot);
    void UseSkill(int skillId, Vector3? targetPosition = null, int? targetEntityId = null);
    void SetAimTarget(int entityId);

    event Action<DamageEvent>? OnDamageDealt;
    event Action<DamageEvent>? OnDamageReceived;
    event Action<int>? OnKill;
}

/// <summary>
/// 基地建造服务。
/// </summary>
public interface IBaseBuildingService
{
    void EnterBuildMode(int buildingTypeId);
    void ExitBuildMode();
    bool CanPlaceAt(Vector3 position, Quaternion rotation);
    bool PlaceBuilding(Vector3 position, Quaternion rotation);
    bool DestroyBuilding(int buildingEntityId);
    bool UpgradeBuilding(int buildingEntityId);
    IReadOnlyList<BuildingTypeInfo> GetAvailableBuildingTypes();

    event Action<int>? OnBuildingPlaced;
    event Action<int>? OnBuildingDestroyed;
}

/// <summary>
/// 太空/宇宙飞船服务。
/// </summary>
public interface ISpaceService
{
    int CurrentSpacecraftId { get; }
    SpaceFlightPhase FlightPhase { get; }
    float Altitude { get; }
    float OrbitalVelocity { get; }
    float RemainingDeltaV { get; }

    bool InitiateLaunch();
    bool Abort();
    bool PerformStaging();
    void SendSpacecraftInput(SpacecraftInputData input);
    bool ConfigureSpacecraft(SpacecraftConfig config);

    // ═══ 轨道查询 ═══
    OrbitalParams GetOrbitalParams();
    bool IsInStableOrbit();

    event Action<SpaceFlightPhase>? OnPhaseChanged;
    event Action<OrbitalEvent>? OnOrbitalEvent;
}

