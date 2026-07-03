using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Systems.Sync;

namespace Ark;

public partial class GameBootstrap
{
    // ═══════════════════════════════════════════════════════════════════════
    //   Phase 3 Sync System Receivers
    //   将 ECS → Bootstrap 编排层的拉取改为 Sync System 推送；
    //   下方两个轻量私有类充当接收器，把最近一帧缓存到 Bootstrap 字段，
    //   后续 UpdateHud / UpdatePerfHud / UpdateNetworkRocketTelemetry 直接消费缓存。
    // ═══════════════════════════════════════════════════════════════════════

    private PlayerHudFrame _lastPlayerHudFrame;
    private bool _hasLastPlayerHudFrame;

    private RocketTelemetryFrame _lastRocketTelemetryFrame;
    private bool _hasLastRocketTelemetryFrame;

    private PlayerHudReceiver? _playerHudReceiver;
    private RocketTelemetryReceiver? _rocketTelemetryReceiver;

    private void RegisterHudSyncReceivers()
    {
        if (_player != null && _playerHudSync != null && _playerHudReceiver == null)
        {
            _playerHudReceiver = new PlayerHudReceiver(this);
            _playerHudSync.Register(_playerHudReceiver);
        }

        if (_rocketTelemetrySync != null && _rocketTelemetryReceiver == null)
        {
            _rocketTelemetryReceiver = new RocketTelemetryReceiver(this);
            _rocketTelemetrySync.Register(_rocketTelemetryReceiver);
        }
    }

    private void UnregisterHudSyncReceivers()
    {
        if (_playerHudReceiver != null)
        {
            _playerHudSync?.Unregister(_playerHudReceiver);
            _playerHudReceiver = null;
        }

        if (_rocketTelemetryReceiver != null)
        {
            _rocketTelemetrySync?.Unregister(_rocketTelemetryReceiver);
            _rocketTelemetryReceiver = null;
        }
    }

    private sealed class PlayerHudReceiver : IPlayerHudReceiver
    {
        private readonly GameBootstrap _owner;
        public PlayerHudReceiver(GameBootstrap owner) { _owner = owner; }
        public Entity PlayerEntity => _owner._player?.Entity ?? default;

        public void OnPlayerHudPushed(in PlayerHudFrame frame)
        {
            _owner._lastPlayerHudFrame = frame;
            _owner._hasLastPlayerHudFrame = true;
        }
    }

    private sealed class RocketTelemetryReceiver : IRocketTelemetryReceiver
    {
        private readonly GameBootstrap _owner;
        public RocketTelemetryReceiver(GameBootstrap owner) { _owner = owner; }

        public Entity RocketEntity
        {
            get
            {
                if (_owner._activeRocketNetworkId == System.Guid.Empty || _owner._remoteWorldEcsCache == null)
                    return default;
                if (!_owner._remoteWorldEcsCache.TryGetEcsEntityId(_owner._activeRocketNetworkId, out var ecsEntityId))
                    return default;
                return _owner._store.GetEntityById(ecsEntityId);
            }
        }

        public void OnRocketTelemetryPushed(in RocketTelemetryFrame frame)
        {
            _owner._lastRocketTelemetryFrame = frame;
            _owner._hasLastRocketTelemetryFrame = true;
        }
    }
}
