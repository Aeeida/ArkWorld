namespace Ark.Systems.Sync;

/// <summary>
/// Phase 3 推送系统的全局访问点。由 Bootstrap 在初始化阶段填充；
/// Node 表现层（CharacterBody3D / HUD / Camera）通过此入口注册自身以接收 ECS 推送。
/// 取代各 Node 在 _Process 中自调用 <c>Entity.TryGetComponent</c> 的反向耦合。
/// </summary>
public static class SyncSystemRegistry
{
    public static CharacterPresentationSyncSystem? CharacterPresentation { get; private set; }
    public static VehicleHudSyncSystem? VehicleHud { get; private set; }
    public static LocalControlSyncSystem? LocalControl { get; private set; }
    public static PlayerHudSyncSystem? PlayerHud { get; private set; }
    public static RocketTelemetrySyncSystem? RocketTelemetry { get; private set; }

    public static void Set(
        CharacterPresentationSyncSystem? character,
        VehicleHudSyncSystem? hud,
        LocalControlSyncSystem? control,
        PlayerHudSyncSystem? playerHud = null,
        RocketTelemetrySyncSystem? rocketTelemetry = null)
    {
        CharacterPresentation = character;
        VehicleHud = hud;
        LocalControl = control;
        PlayerHud = playerHud;
        RocketTelemetry = rocketTelemetry;
    }

    public static void Clear()
    {
        CharacterPresentation = null;
        VehicleHud = null;
        LocalControl = null;
        PlayerHud = null;
        RocketTelemetry = null;
    }
}
