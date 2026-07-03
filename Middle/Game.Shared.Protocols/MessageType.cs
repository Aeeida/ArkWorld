namespace Game.Shared.Protocols;

public enum MessageType : ushort
{
    LoginRequest = 1001,
    LoginResponse = 1002,
    EnterWorldRequest = 2001,
    EnterWorldResponse = 2002,
    PlayerMoveRequest = 3001,
    PlayerMoveUpdate = 3002,
    CombatActionRequest = 4001,
    CombatActionResponse = 4002,
    ChatMessage = 5001,
    MarketOrderRequest = 6001,
    MarketOrderResponse = 6002,

    // ── 新增：服务端权威高频包 ──
    VehicleInputRequest = 7001,
    VehicleStateUpdate = 7002,
    SpacecraftInputRequest = 7101,
    SpacecraftStateUpdate = 7102,
    TerrainChunkRequest = 8001,
    TerrainChunkResponse = 8002,
    WeatherUpdate = 8101,
    EnvironmentUpdate = 8102,
    NearbyEntitiesUpdate = 9001,
    SquadMemberUpdate = 9101,
    PartyUpdate = 9102,
    BuildingStateUpdate = 9201,

    // ── 服务端权威：玩法模式 & 技能/功能系统 ──
    ModeChangeRequest = 10001,
    ModeChangeResponse = 10002,
    AbilityUseRequest = 10101,
    AbilityUseResponse = 10102,
    AbilityBarSyncRequest = 10201,
    AbilityBarSyncResponse = 10202,
    TargetFrameUpdate = 10301,
    PartyFrameUpdate = 10302,
    VehicleHudUpdate = 10401,
    SpaceshipHudUpdate = 10501,
    FleetCommandUpdate = 10601,
    AbilityCooldownUpdate = 10701
}
