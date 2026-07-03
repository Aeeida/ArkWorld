using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using Ark.Ecs.Components;
using Ark.Services;
using Ark.Events;
using Ark.Bridge.Features.Space;
using Ark.UI;

namespace Ark;

public partial class GameBootstrap
{
    // ═══════════════════════════════════════════════════════════════════════
    //                          事件回调
    // ═══════════════════════════════════════════════════════════════════════

    private void OnVehicleSpawnRequested(int vehicleDefId, System.Numerics.Vector3 spawnPos)
    {
        // 网络模式：路由到服务端
        if (GameServices.IsNetworkMode)
        {
            _ecsAuth.CreateRequest(new NetworkVehicleSpawnRequest
            {
                VehicleDefId = vehicleDefId,
                SpawnX = spawnPos.X,
                SpawnY = spawnPos.Y,
                SpawnZ = spawnPos.Z,
            });
            GD.Print($"[GameBootstrap] Vehicle spawn requested via server: def={vehicleDefId}");
            return;
        }

        if (_combatGameplay == null) return;
        var vehicleEntity = _combatGameplay.SpawnVehicle(vehicleDefId, spawnPos, System.Numerics.Quaternion.Identity);
        if (!vehicleEntity.IsNull)
        {
            _weaponVisuals?.SpawnVehicleVisual(vehicleEntity.Id, new Vector3(spawnPos.X, spawnPos.Y, spawnPos.Z), vehicleDefId);
            GD.Print($"[GameBootstrap] Spawned vehicle def={vehicleDefId} entity={vehicleEntity.Id}");
        }
    }

    /// <summary>火箭发射台被授权玩家点击 → 打开火箭设计 UI 面板（不切换环境）。</summary>
    private void OnLaunchPadActivated(int entityId, Vector3 worldPos)
    {
        GD.Print($"[GameBootstrap] Launch pad activated: entity={entityId} at {worldPos}");
        _rocketPanel?.ShowForPad(entityId, worldPos);
        // 注意：不再切换到 Space 模式（避免暗化环境），仅打开设计面板
    }

    /// <summary>VAB 设计完成 → 显示发射控制面板（不切换环境，火箭仍在地面）。</summary>
    private void OnRocketDesignComplete(int padEntityId, Vector3 padPos, RocketConfig config)
    {
        GD.Print($"[GameBootstrap] Rocket design complete — placing on pad {padEntityId}");
        _rocketPanel?.Hide();
        _activeRocketNetworkId = System.Guid.Empty;
        SetActiveRocketControlState(System.Guid.Empty);
        _activeRocketConfig = config;
        _activeRocketConfigJson = JsonSerializer.Serialize(config);

        if (GameServices.IsNetworkMode)
        {
            if (TryGetRemoteEntityNetworkId(padEntityId, out var launchPadNetworkId))
            {
                _ecsAuth.CreateRequest(new NetworkRocketAssemblyRequest
                {
                    LaunchPadNetworkId = launchPadNetworkId,
                    RocketConfigJson = _activeRocketConfigJson,
                });
            }
            else
            {
                GD.PrintErr($"[GameBootstrap] Launch pad network id not found for local entity {padEntityId}");
            }
        }

        if (!GameServices.IsNetworkMode)
            _launchController?.PlaceOnPad(padPos, config);
        _launchControlPanel?.Show();

        // 不切换到太空模式 — 火箭仍在地面，保持当前环境
        // 太空大气将在火箭升空到足够高度时由遥测系统触发

        if (!GameServices.IsNetworkMode && _rocketCamera != null && _launchController?.RocketBody != null)
            _rocketCamera.Activate(_launchController.RocketBody);
    }

    /// <summary>发射控制面板「发射」按钮 → 开始倒计时。</summary>
    private void OnLaunchControlFire()
    {
        GD.Print("[GameBootstrap] Launch control: FIRE");

        if (GameServices.IsNetworkMode)
        {
            if (_activeRocketNetworkId != System.Guid.Empty)
            {
                _player?.BeginSpacecraftControl();
                _ecsAuth.CreateRequest(new NetworkRocketLaunchRequest
                {
                    RocketNetworkId = _activeRocketNetworkId,
                });
            }
            else
                GD.PrintErr("[GameBootstrap] Launch requested before server rocket id was confirmed");
            _launchControlPanel?.MarkLaunched();
            return;
        }

        _launchController?.BeginLaunch();
        _launchControlPanel?.MarkLaunched();
    }

    /// <summary>发射控制面板「中止」。</summary>
    private void OnLaunchControlAbort()
    {
        GD.Print("[GameBootstrap] Launch control: ABORT");
        if (GameServices.IsNetworkMode)
        {
            _hasPredictedRocketPose = false;
            _predictedRocketVelocity = Vector3.Zero;
            _player?.EndSpacecraftControl();
            _rocketCamera?.Deactivate();
            return;
        }
        _launchController?.Abort();
    }

    private void TryAttachRocketCameraToRemoteNode()
    {
        if (_activeRocketNetworkId == System.Guid.Empty || _rocketCamera == null || _remotePlayerBridge == null)
            return;
        if (_remoteWorldEcsCache == null)
            return;

        if (!_remoteWorldEcsCache.TryGetEcsEntityId(_activeRocketNetworkId, out var ecsEntityId))
            return;
        if (!_remoteWorldEcsCache.TryGetSnapshotEntityId(ecsEntityId, out var snapshotEntityId))
            return;

        if (_remotePlayerBridge.TryGetNode(snapshotEntityId, out var node) && node != null)
            _rocketCamera.Activate(node);
    }

    /// <summary>发射控制面板「分级」。</summary>
    private void OnLaunchControlStage()
    {
        GD.Print("[GameBootstrap] Launch control: STAGE");
        if (GameServices.IsNetworkMode)
        {
            // 网络模式：分级通过 SpacecraftInput actionFlags 发送（键盘 R 键）
            GD.Print("[GameBootstrap] Network mode: staging routed through SpacecraftInput");
            return;
        }
        _launchController?.PerformStaging();
    }

    /// <summary>油门变更。</summary>
    private void OnThrottleChanged(float value)
    {
        if (GameServices.IsNetworkMode)
        {
            _player?.SetSpacecraftThrottle(value);
            return;
        }
        _launchController?.SetThrottle(value);
    }

    /// <summary>遥测数据更新 → 转发到发射控制面板。</summary>
    private void OnTelemetryUpdate(TelemetryData data)
    {
        _launchControlPanel?.UpdateTelemetry(data);
    }

    /// <summary>配装面板关闭（取消设计）。</summary>
    private void OnRocketPanelClosed()
    {
        GD.Print("[GameBootstrap] Rocket panel closed");
        // 确保鼠标重新锁定到 TPS 模式
        _player?.ForceMouseCaptured();
        Input.MouseMode = Input.MouseModeEnum.Captured;

        // 如果在太空模式且火箭没有在飞行，退回战斗模式
        if (_currentMode == GameplayMode.Space && _launchController != null && !_launchController.IsActive)
            OnGameplayModeSelected(GameplayMode.Combat);
    }

    /// <summary>发射序列完成（火箭升空到太空）。</summary>
    private void OnLaunchSequenceComplete()
    {
        if (GameServices.IsNetworkMode)
            throw new InvalidOperationException("[NetworkGuard] OnLaunchSequenceComplete called in network mode — local LaunchSequenceController must not be used.");
        GD.Print("[GameBootstrap] Launch sequence complete — entering orbit");
        _space?.InitiateLaunch();
    }

    /// <summary>火箭安全着陆。</summary>
    private void OnRocketLandedSafe()
    {
        GD.Print("[GameBootstrap] Rocket landed safely");
    }

    /// <summary>飞行报告就绪 → 显示报告面板。</summary>
    private void OnFlightReportReady(FlightReport report)
    {
        GD.Print($"[GameBootstrap] Flight report ready — success={report.Success}");
        _launchControlPanel?.Hide();
        _rocketCamera?.Deactivate();
        _hasPredictedRocketPose = false;
        _predictedRocketVelocity = Vector3.Zero;
        _player?.EndSpacecraftControl();
        _flightReportPanel?.ShowReport(report);
    }

    /// <summary>飞行报告面板「退出到 XX 模式」按钮。</summary>
    private void OnFlightReportExitSelected(GameplayMode targetMode)
    {
        GD.Print($"[GameBootstrap] Flight report exit → {targetMode}");
        ExitRocketMode();
        OnGameplayModeSelected(targetMode);
    }

    /// <summary>从玩法切换面板强制退出太空/火箭模式。</summary>
    private void OnForceExitSpaceMode()
    {
        GD.Print("[GameBootstrap] Force exit space mode");
        ExitRocketMode();
    }

    /// <summary>完全退出火箭设计/发射模式，清理所有状态。</summary>
    private void ExitRocketMode()
    {
        _activeRocketNetworkId = System.Guid.Empty;
        SetActiveRocketControlState(System.Guid.Empty);
        _activeRocketConfig = null;
        _activeRocketConfigJson = string.Empty;
        _hasPredictedRocketPose = false;
        _predictedRocketVelocity = Vector3.Zero;
        _rocketPanel?.Hide();
        _launchControlPanel?.Hide();
        _flightReportPanel?.Hide();
        _rocketCamera?.Deactivate();
        _player?.EndSpacecraftControl();

        if (!GameServices.IsNetworkMode && _launchController != null && _launchController.IsActive)
            _launchController.Abort();
        if (!GameServices.IsNetworkMode)
            _launchController?.CleanupVisuals();
    }

    private void SetActiveRocketControlState(System.Guid rocketNetworkId)
    {
        if (_remoteWorldEcsCache is null)
            return;

        int localEntityId = _remoteWorldEcsCache.LocalPresentationEntityId;
        if (localEntityId <= 0)
            return;

        var localEntity = _store.GetEntityById(localEntityId);
        if (localEntity.IsNull)
            return;

        _ecsAuth.Write(localEntity, new RemoteRocketControlState
        {
            ActiveRocketNetworkId = rocketNetworkId,
            HasActiveRocket = rocketNetworkId != System.Guid.Empty ? (byte)1 : (byte)0,
        });
        _player?.SyncLocalControlStateToEcs();
    }
}
