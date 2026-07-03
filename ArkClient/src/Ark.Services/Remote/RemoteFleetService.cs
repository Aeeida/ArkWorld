using System;
using System.Collections.Generic;
using System.Threading;
using Game.Shared.Core.DTOs;

namespace Ark.Services.Remote;

/// <summary>
/// 远程舰队/主权服务 — 舰队编队、指挥、主权争夺，所有操作通过 SignalR RPC。
/// </summary>
public sealed class RemoteFleetService
{
    private readonly Networking.NetworkManager _network;
    private readonly Guid _playerId;

    public FleetDto? CurrentFleet { get; private set; }
    public IReadOnlyList<SovereigntyDto>? CachedSovereigntyMap { get; private set; }

    public event Action<FleetDto>? OnFleetUpdated;
    public event Action<IReadOnlyList<SovereigntyDto>>? OnSovereigntyMapRefreshed;
    public event Action<string>? OnFleetMessage;

    public RemoteFleetService(Networking.NetworkManager network, Guid playerId)
    {
        _network = network;
        _playerId = playerId;
    }

    public async void FormFleet(FormFleetCommandDto cmd)
    {
        try
        {
            var result = await _network.SignalR.FormFleetAsync(cmd, CancellationToken.None);
            if (result.Success && result.FleetId.HasValue)
            {
                var fleet = await _network.SignalR.GetFleetAsync(result.FleetId.Value, CancellationToken.None);
                if (fleet is not null)
                {
                    CurrentFleet = fleet;
                    OnFleetUpdated?.Invoke(fleet);
                }
            }
            else
            {
                OnFleetMessage?.Invoke($"Form fleet failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteFleet] FormFleet failed: {ex.Message}");
        }
    }

    public async void CommandFleet(CommandFleetCommandDto cmd)
    {
        try
        {
            var result = await _network.SignalR.CommandFleetAsync(cmd, CancellationToken.None);
            if (!result.Success)
                OnFleetMessage?.Invoke($"Command failed: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteFleet] CommandFleet failed: {ex.Message}");
        }
    }

    public async void ClaimSovereignty(ClaimSovereigntyCommandDto cmd)
    {
        try
        {
            var result = await _network.SignalR.ClaimSovereigntyAsync(cmd, CancellationToken.None);
            if (result.Success)
                OnFleetMessage?.Invoke($"Sovereignty claimed in {result.SolarSystemId}");
            else
                OnFleetMessage?.Invoke($"Claim failed: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteFleet] ClaimSovereignty failed: {ex.Message}");
        }
    }

    public async void FetchSovereigntyMap()
    {
        try
        {
            var map = await _network.SignalR.GetSovereigntyMapAsync(CancellationToken.None);
            CachedSovereigntyMap = map;
            OnSovereigntyMapRefreshed?.Invoke(map);
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteFleet] FetchSovereigntyMap failed: {ex.Message}");
        }
    }

    // ═══ 服务端事件回调 ═══

    public void HandleFleetCreated(Guid fleetId, string fleetName, Guid leaderId)
        => OnFleetMessage?.Invoke($"Fleet '{fleetName}' created");

    public void HandleFleetMemberJoined(Guid fleetId, Guid playerId)
        => OnFleetMessage?.Invoke($"Player joined fleet");

    public void HandleFleetBattleStarted(Guid fleetId, string solarSystemId, int attackerCount, int defenderCount)
        => OnFleetMessage?.Invoke($"Fleet battle started in {solarSystemId}: {attackerCount} vs {defenderCount}");
}
