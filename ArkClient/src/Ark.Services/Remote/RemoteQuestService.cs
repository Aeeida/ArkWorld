using System;
using System.Threading;
using Game.Shared.Core.DTOs;

namespace Ark.Services.Remote;

/// <summary>
/// 远程任务工具 — 仅保留显式 RPC/刷新辅助。
/// </summary>
public sealed class RemoteQuestService
{
    private readonly Networking.NetworkManager _network;
    private System.Guid _playerId;

    public RemoteQuestService(Networking.NetworkManager network, System.Guid playerId)
    {
        _network = network;
        _playerId = playerId;
    }

    public void SetPlayerId(System.Guid playerId) => _playerId = playerId;

    /// <summary>从服务端拉取活跃任务列表。</summary>
    public async void RefreshFromServer()
    {
        try
        {
            var quests = await _network.SignalR.GetActiveQuestsAsync(_playerId, CancellationToken.None);
            Ark.Services.GameServices.ServerEventEcsProjection?.EnqueueQuestList(quests);
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteQuest] RefreshFromServer failed: {ex.Message}");
        }
    }

}
