using System;
using System.Threading;
using Game.Shared.Core.DTOs;

namespace Ark.Services.Remote;

/// <summary>
/// 远程库存工具 — 仅保留显式 RPC/刷新辅助。
/// </summary>
public sealed class RemoteInventoryService
{
    private readonly Networking.NetworkManager _network;
    private System.Guid _playerId;

    public RemoteInventoryService(Networking.NetworkManager network, System.Guid playerId)
    {
        _network = network;
        _playerId = playerId;
    }

    public void SetPlayerId(System.Guid playerId) => _playerId = playerId;

    /// <summary>
    /// 从服务端拉取完整库存（登录 / 进入世界后调用）。
    /// </summary>
    public async void RefreshFromServer()
    {
        try
        {
            var dto = await _network.SignalR.GetInventoryAsync(_playerId, CancellationToken.None);
            Ark.Services.GameServices.ServerEventEcsProjection?.EnqueueInventory(dto);
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteInventory] RefreshFromServer failed: {ex.Message}");
        }
    }

}
