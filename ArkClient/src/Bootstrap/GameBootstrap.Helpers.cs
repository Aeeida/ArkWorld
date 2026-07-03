using System;
using Godot;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Events;
using Ark.Networking;
using Ark.Services;
using Ark.Shared.Data;
using Ark.World.Core;
using Ark.Services.Remote;

namespace Ark;

public partial class GameBootstrap
{
    // ═══════════════════════════════════════════════════════════════════════
    //                          每帧驱动（薄委托）
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateHud()
    {
        if (_hudController == null || _player == null) return;

        // Phase 3: 玩家 ECS 组件来自 PlayerHudSyncSystem 推送的最近一帧。
        ref readonly var playerFrame = ref _lastPlayerHudFrame;

        if (GameServices.IsNetworkMode
            && _hasLastPlayerHudFrame
            && playerFrame.HasLocalControl
            && (LocalControlSource)playerFrame.LocalControl.ControlSource == LocalControlSource.VehicleSeat)
        {
            var localControlState = playerFrame.LocalControl;
            if (_remoteWorldEcsCache != null
                && localControlState.ControlledSnapshotEntityId > 0
                && _remoteWorldEcsCache.TryGetEcsEntityId(localControlState.ControlledSnapshotEntityId, out var ecsVehicleId))
            {
                var vehicleEntity = _store.GetEntityById(ecsVehicleId);
                if (!vehicleEntity.IsNull && vehicleEntity.TryGetComponent<RemoteEntityState>(out var remoteState))
                {
                    var vehicleDef = _combatData.VehicleDefs.Get(remoteState.TypeId);
                    if (vehicleDef is { } vd && vd.Seats.Length > 0)
                    {
                        int seatIndex = localControlState.SeatIndex;
                        seatIndex = Mathf.Clamp(seatIndex, 0, vd.Seats.Length - 1);
                        var seat = vd.Seats[seatIndex];
                        string seatName = ((Ark.Shared.Data.SeatType)localControlState.SeatType) switch
                        {
                            Ark.Shared.Data.SeatType.Driver => "驾驶位",
                            Ark.Shared.Data.SeatType.Gunner => "炮手位",
                            _ => "乘客位"
                        };
                        int weaponDefId = seat.HasWeapon ? seat.WeaponDefId : 0;
                        string weaponName = weaponDefId > 0
                            ? _combatData.WeaponDefs.Get(weaponDefId)?.Name ?? $"Weapon #{weaponDefId}"
                            : "无载具武器";
                        int currentMag = playerFrame.HasAmmo ? playerFrame.Ammo.CurrentMag : 0;
                        int magCap = playerFrame.HasAmmo ? playerFrame.Ammo.MagCapacity : 0;
                        int reserve = playerFrame.HasAmmo ? playerFrame.Ammo.ReserveAmmo : 0;
                        string debugInfo = string.Empty;
                        if (playerFrame.HasMountedWeapon)
                        {
                            var mountedRuntime = playerFrame.MountedWeapon;
                            debugInfo = $"HEAT {mountedRuntime.Heat:P0}  |  CYCLE {mountedRuntime.FireCycleRemaining:F2}s";
                            if (mountedRuntime.IsReloading != 0)
                                debugInfo += $"  |  RELOAD {mountedRuntime.ReloadRemaining:F2}s";
                            if (mountedRuntime.IsMaintaining != 0)
                                debugInfo += $"  |  MAINT {mountedRuntime.MaintenanceRemaining:F2}s";
                            if (mountedRuntime.RepairStepCount > 0)
                                debugInfo += $"  |  STEP {mountedRuntime.RepairStep}/{mountedRuntime.RepairStepCount} {mountedRuntime.OperationProgress:P0} MAT {mountedRuntime.MaterialUnits} SK {mountedRuntime.SkillScalar:F2}";
                            else if (mountedRuntime.IsOverheated != 0)
                                debugInfo += "  |  OVERHEATED";
                            else if (mountedRuntime.FaultCode == 1)
                                debugInfo += "  |  JAMMED";
                            else if (mountedRuntime.FaultCode == 3)
                                debugInfo += "  |  FEED";
                            else if (mountedRuntime.FaultCode == 4)
                                debugInfo += "  |  ALIGN";

                            _seatWeaponPanel?.UpdatePanel(
                                weaponName,
                                mountedRuntime.Heat,
                                mountedRuntime.ReloadNormalized,
                                mountedRuntime.FireCycleRemaining,
                                mountedRuntime.ReloadRemaining,
                                mountedRuntime.MaintenanceLevel,
                                mountedRuntime.MaintenanceRemaining,
                                mountedRuntime.OperationProgress,
                                mountedRuntime.SkillScalar,
                                mountedRuntime.RepairStep,
                                mountedRuntime.RepairStepCount,
                                mountedRuntime.MaterialUnits,
                                mountedRuntime.FaultCode);
                        }
                        else
                        {
                            _seatWeaponPanel?.HidePanel();
                        }
                        _selectionHUD?.UpdateCharacterInfo($"队长 / {seatName}", weaponName, currentMag, magCap, reserve, debugInfo);
                        return;
                    }
                }
            }
        }

        int activeId;
        string charName;
        if (_squad != null && _squad.ActiveSlot > 0)
        {
            var member = _squad.GetMember(_squad.ActiveSlot);
            activeId = member?.Entity.Id ?? 0;
            charName = $"队员 F{_squad.ActiveSlot}";
        }
        else
        {
            activeId = _player.Entity.Id;
            charName = "队长";
        }
        _hudController.Update(activeId, charName);
        _seatWeaponPanel?.HidePanel();
    }

    private void DriveSquadCombat()
    {
        if (_squadCombat == null || _squad == null || _player == null) return;

        if (!_player.IsMouseCaptured) return;

        var camera = _player.Camera;
        var aimDir = camera != null
            ? -camera.GlobalTransform.Basis.Z
            : -_player.GlobalTransform.Basis.Z;
        var sysDir = new System.Numerics.Vector3(aimDir.X, aimDir.Y, aimDir.Z);

        int count = _squad.MemberCount;
        Span<int> ids = stackalloc int[count];
        Span<System.Numerics.Vector3> positions = stackalloc System.Numerics.Vector3[count];

        for (int i = 0; i < count; i++)
        {
            var m = _squad.GetMember(i + 1);
            ids[i] = m?.Entity.Id ?? 0;
            var p = m?.GlobalPosition ?? Vector3.Zero;
            positions[i] = new System.Numerics.Vector3(p.X, p.Y, p.Z);
        }
        _squadCombat.Update(_player.Entity.Id, sysDir, ids, positions, _squad.ActiveSlot);
    }

    private void DispatchGpuWork(float dt)
    {
        if (_gpuMovement is not { IsInitialized: true }) return;
        var (bytes, count, d) = _gpuMovement.PrepareDispatch(dt);
        if (count <= 0) return;
        RenderingServer.CallOnRenderThread(Callable.From(() =>
        {
            var rd = RenderingServer.GetRenderingDevice();
            if (rd != null) _gpuMovement.DispatchOnRenderThread(bytes, count, d, rd);
        }));
    }

    private void UpdatePerfHud(double delta)
    {
        if (_perfHud == null) return;
        _perfHud.EntityCount = _store.Count;

        string squadInfo = _squad != null
            ? $"Squad: {_squad.MemberCount + 1} [F{_squad.ActiveSlot + 1}]"
            : "Squad: OFF";

        string worldInfo = _worldEnvManager?.IsInitialized == true
            ? $"Terrain: Custom ({_worldEnvManager.LoadedChunkCount} chunks)  |  " +
              $"Weather: {_worldEnvManager.WeatherState.CurrentType}  |  " +
              $"Day {_worldEnvManager.TimeState.Day} {_worldEnvManager.TimeState.Period}"
            : "World: OFF";

        string netInfo = GameServices.IsNetworkEnabled
            ? $"Net: {GameServices.NetworkManager?.ConnectionState ?? Ark.Networking.NetworkConnectionState.Disconnected}  |  Srv: NETWORK"
            : "Net: OFF";

        string mountedDebug = string.Empty;
        if (_player != null && _hasLastPlayerHudFrame && _lastPlayerHudFrame.HasMountedWeapon)
        {
            var mountedWeapon = _lastPlayerHudFrame.MountedWeapon;
            mountedDebug = $"  |  MountHeat {mountedWeapon.Heat:P0} Cycle {mountedWeapon.FireCycleRemaining:F2}s Reload {mountedWeapon.ReloadRemaining:F2}s Maint {mountedWeapon.MaintenanceRemaining:F2}s";
        }

        string riderBudgetDebug = string.Empty;
        if (_player != null && _hasLastPlayerHudFrame && _lastPlayerHudFrame.HasRemoteAnimation)
        {
            var riderAnim = _lastPlayerHudFrame.RemoteAnimation;
            riderBudgetDebug = $"  |  RiderAnim {riderAnim.ResourceFragmentId}:{riderAnim.NetworkBudgetBytes}B H{riderAnim.CacheHits}/M{riderAnim.CacheMisses} S{riderAnim.StreamingState}";
        }

        string remoteServiceDebug = string.Empty;
        if (_player != null && _hasLastPlayerHudFrame)
        {
            if (_lastPlayerHudFrame.HasRemoteInventory)
            {
                var inventoryState = _lastPlayerHudFrame.RemoteInventory;
                remoteServiceDebug += $"  |  Inv {inventoryState.OccupiedSlotCount}/{inventoryState.SlotCount} Items {inventoryState.TotalItemCount}";
            }
            if (_lastPlayerHudFrame.HasRemoteQuest)
            {
                var questState = _lastPlayerHudFrame.RemoteQuest;
                remoteServiceDebug += $"  |  Quest {questState.ActiveQuestCount}/{questState.AvailableQuestCount}";
            }
            if (_lastPlayerHudFrame.HasRemoteWorldService)
            {
                var worldState = _lastPlayerHudFrame.RemoteWorldService;
                remoteServiceDebug += $"  |  AOI {worldState.NearbyEntityCount} Party {worldState.PartyMemberCount} Wx {worldState.WeatherId}:{worldState.WeatherIntensity:F2}";
            }
        }

        _perfHud.ExtraInfo =
            $"Mode: {_currentMode}  |  " +
            $"{squadInfo}  |  " +
            $"{worldInfo}  |  " +
            $"GPU Movement: {(_gpuMovement?.IsInitialized == true ? "ON" : "OFF")}  |  " +
            netInfo +
            mountedDebug +
            riderBudgetDebug +
            remoteServiceDebug;
        _perfHud.Update(delta);

        // 网络信息 HUD
        _networkInfoHud?.Update(delta);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          地形辅助
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 将玩家和队员放置到地形表面 — 防止生成在地形下方导致无限坠落。
    /// 必须在 InitializeWorldEnvironment 之后调用。
    /// </summary>
    private void PlacePlayerOnTerrain()
    {
        if (_player == null || _worldEnvManager == null) return;
        if (!_worldEnvManager.IsInitialized)
            throw new InvalidOperationException("[GameBootstrap] WorldEnvironmentManager is not initialized from server seed.");

       float spawnX = GameServices.LastSpawnX;
        float spawnZ = GameServices.LastSpawnZ;
        float terrainY = _worldEnvManager.SampleTerrainHeight(spawnX, spawnZ);
      float safeY = MathF.Max(GameServices.LastSpawnY, terrainY + WorldConstants.SpawnHeightMargin);

        _player.GlobalPosition = new Vector3(spawnX, safeY, spawnZ);
        if (!_player.Entity.IsNull)
            _ecsAuth.Write(_player.Entity, new WorldPosition { X = spawnX, Y = safeY, Z = spawnZ });

        GD.Print($"[GameBootstrap] Player spawned at ({spawnX:F0}, {safeY:F1}, {spawnZ:F0})  terrain={terrainY:F1}");

       // 网络模式下不再生成/摆放本地小队，避免假象实体。
        if (_squad != null && !GameServices.IsNetworkMode)
        {
            for (int i = 1; i <= _squad.MemberCount; i++)
            {
                var member = _squad.GetMember(i);
                if (member is Node3D memberNode)
                {
                    float mx = spawnX + (i % 2 == 0 ? 2f : -2f) * ((i + 1) / 2);
                    float mz = spawnZ + 2f * ((i + 1) / 2);
                    float my = _worldEnvManager.SampleTerrainHeight(mx, mz) + WorldConstants.SpawnHeightMargin;
                    memberNode.GlobalPosition = new Vector3(mx, my, mz);
                }
            }
        }
    }

    /// <summary>
    /// 将所有带 WorldPosition 的 ECS 实体的 Y 坐标校正到地形表面。
    /// </summary>
    private void SnapEntitiesToTerrain()
    {
        if (_worldEnvManager == null || !_worldEnvManager.IsInitialized) return;

        int corrected = 0;
        _store.Query<WorldPosition>().ForEachEntity((ref WorldPosition pos, Entity _) =>
        {
            float terrainY = _worldEnvManager.SampleTerrainHeight(pos.X, pos.Z);
            pos.Y = terrainY;
            corrected++;
        });

        GD.Print($"[GameBootstrap] Snapped {corrected} entities to terrain surface");
    }

    /// <summary>启用/禁用 Node 子系统。</summary>
    private static void SetNodeActive(Node? node, bool active)
    {
        if (node == null) return;
        node.ProcessMode = active
            ? ProcessModeEnum.Inherit
            : ProcessModeEnum.Disabled;
    }

    /// <summary>
    /// 根据当前活动摄像头位置更新地形加载。
    /// 摄像头可能来自：玩家 TPS 相机、全局相机、火箭相机等。
    /// 高度越高，加载范围越大。
    /// </summary>
    private void UpdateTerrainFromCamera()
    {
        if (_worldEnvManager == null) return;

        // ── 锚定玩家/小队位置，防止摄像头飞远时地形卸载导致角色坠落 ──
        if (_player != null)
        {
            var playerPos = _player.GlobalPosition;
            _worldEnvManager.AddAnchoredRegion(playerPos.X, playerPos.Z, 3);
        }

        // 优先使用当前活动的摄像头位置
        Camera3D? activeCam = GetViewport().GetCamera3D();
        if (activeCam != null)
        {
            var camPos = activeCam.GlobalPosition;
            float altitude = camPos.Y;

            // 如果有地形查询，计算摄像头相对地表的高度
            if (GameServices.Terrain != null)
            {
                float terrainY = GameServices.Terrain.SampleHeight(camPos.X, camPos.Z);
                altitude = camPos.Y - terrainY;
            }

            _worldEnvManager.UpdateCameraPosition(camPos, altitude);
        }
        else if (_player != null)
        {
            // 后备：使用玩家位置
            _worldEnvManager.UpdatePlayerPosition(_player.GlobalPosition);
        }
    }

    private void SetWorldEntryGate(bool active)
    {
        _worldEntryCompleted = !active;

        if (_modeSwitchPanel != null)
            _modeSwitchPanel.Visible = !active;
        if (_perfHud != null)
            _perfHud.Visible = !active;
        if (_selectionHUD != null)
            _selectionHUD.Visible = !active && _currentMode != GameplayMode.Space;
        if (_modeHudManager != null)
            _modeHudManager.SetHudVisible(!active);

        SetNodeActive(_buildPlacement, !active && (_currentMode == GameplayMode.Life || _currentMode == GameplayMode.Combat));
        SetNodeActive(_buildingVisuals, !active && (_currentMode == GameplayMode.Life || _currentMode == GameplayMode.Combat));
        SetNodeActive(_weaponVisuals, !active && _currentMode == GameplayMode.Combat);
        SetNodeActive(_enemyVisuals, !active && _currentMode == GameplayMode.Combat);

        if (_player != null)
        {
            _player.Visible = !active && _currentMode != GameplayMode.Space;
            _player.ProcessMode = active ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
        }

        if (_squad != null)
        {
            for (int i = 1; i <= _squad.MemberCount; i++)
            {
                var member = _squad.GetMember(i);
                if (member is Node memberNode)
                    memberNode.ProcessMode = active ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
                if (member is Node3D memberNode3D)
                    memberNode3D.Visible = !active && _currentMode != GameplayMode.Space;
            }
        }

        if (active)
        {
            _rocketPanel?.Hide();
            _launchControlPanel?.Hide();
            _flightReportPanel?.Hide();
            _networkInfoHud?.EnterStartupMode();
        }
    }

    private static bool TryGetRemoteEntityNetworkId(int entityId, out System.Guid networkId)
    {
        networkId = System.Guid.Empty;
        return GameServices.RemoteWorldEcsCache?.TryGetNetworkId(entityId, out networkId) == true;
    }

    private void OnWorldEntryCompleted()
    {
        if (_worldEntryCompleted)
            return;
        if (_worldEnvManager == null || !_worldEnvManager.IsInitialized)
            throw new InvalidOperationException("[GameBootstrap] Blocking world entry: server terrain was not applied.");

        PlacePlayerOnTerrain();
        if (!GameServices.IsNetworkMode)
            SnapEntitiesToTerrain();
        ApplyGameplayMode(_currentMode);
        SetWorldEntryGate(false);
        _networkInfoHud?.ExitStartupMode();
        _player?.ForceMouseCaptured();
        Input.MouseMode = Input.MouseModeEnum.Captured;
        GD.Print("[GameBootstrap] World entry gate released");
    }

    private void UploadLocalPlayerState(float dt)
    {
        if (!_worldEntryCompleted || _player == null || !GameServices.IsNetworkEnabled)
            return;

        if (_player.Entity.TryGetComponent<LocalControlState>(out var localControlState))
        {
            if (localControlState.ExternalControlLocked != 0)
                return;
            if ((LocalControlSource)localControlState.ControlSource != LocalControlSource.CharacterDirect)
                return;
        }
        else if (_player.Entity.TryGetComponent<RemoteVehicleOccupantState>(out var remoteVehicleState)
                 && remoteVehicleState.SnapshotVehicleEntityId > 0)
        {
            return;
        }

        if (GameServices.RemotePlayerId == System.Guid.Empty)
            return;

        if (GameServices.NetworkManager?.ConnectionState != NetworkConnectionState.Connected)
            return;

        var controlledEntity = _squad != null && _squad.ActiveSlot > 0
            ? _squad.GetMember(_squad.ActiveSlot)?.Entity ?? _player.Entity
            : _player.Entity;
        if (controlledEntity.IsNull) return;
        if (!controlledEntity.TryGetComponent<WorldPosition>(out var ecsPos)) return;

        var worldPos = new Vector3(ecsPos.X, ecsPos.Y, ecsPos.Z);
        var (moveDirection, moveSpeed) = _player.GetMoveInput();
        var desiredVelocity = moveDirection * moveSpeed;
        Vector3 aimDir;
        if (controlledEntity.TryGetComponent<CombatTarget>(out var target) && target.HasTarget != 0)
        {
            var aimPoint = new Vector3(target.AimPointX, target.AimPointY, target.AimPointZ);
            aimDir = (aimPoint - worldPos).Normalized();
        }
        else if (controlledEntity.TryGetComponent<WorldRotation>(out var ecsRot))
        {
            aimDir = new Quaternion(ecsRot.X, ecsRot.Y, ecsRot.Z, ecsRot.W) * -Vector3.Forward;
        }
        else
        {
            aimDir = _player.GetAimDirection();
        }

        _ecsAuth.Write(controlledEntity, new NetworkPlayerInputCommand
        {
            MoveDirX = desiredVelocity.X,
            MoveDirY = desiredVelocity.Y,
            MoveDirZ = desiredVelocity.Z,
            AimDirX = aimDir.X,
            AimDirY = aimDir.Y,
            AimDirZ = aimDir.Z,
            ActionFlags = 0,
            Timestamp = Time.GetTicksMsec() / 1000f,
            Sequence = ++_playerInputCommandSequence,
        });
    }
}
