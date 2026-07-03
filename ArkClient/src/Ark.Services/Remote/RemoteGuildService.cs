using System;
using System.Threading;
using Game.Shared.Core.DTOs;

namespace Ark.Services.Remote;

/// <summary>
/// 远程公会/社交服务 — 公会创建、加入、聊天，所有操作通过 SignalR RPC。
/// </summary>
public sealed class RemoteGuildService
{
    private readonly Networking.NetworkManager _network;
    private readonly Guid _playerId;

    public GuildInfoDto? CurrentGuild { get; private set; }
    public GetMailDto? CachedMail { get; private set; }

    public event Action<GuildInfoDto>? OnGuildUpdated;
    public event Action<GetMailDto>? OnMailRefreshed;
    public event Action<string>? OnGuildMessage;

    public RemoteGuildService(Networking.NetworkManager network, Guid playerId)
    {
        _network = network;
        _playerId = playerId;
    }

    public async void CreateGuild(CreateGuildCommandDto cmd)
    {
        try
        {
            var result = await _network.SignalR.CreateGuildByCommandAsync(cmd, CancellationToken.None);
            if (result.Success && result.GuildId.HasValue)
            {
                var info = await _network.SignalR.GetGuildInfoAsync(result.GuildId.Value, CancellationToken.None);
                CurrentGuild = info;
                OnGuildUpdated?.Invoke(info);
            }
            else
            {
                OnGuildMessage?.Invoke($"Create guild failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteGuild] CreateGuild failed: {ex.Message}");
        }
    }

    public async void JoinGuild(Guid guildId)
    {
        try
        {
            var success = await _network.SignalR.JoinGuildAsync(guildId, _playerId, CancellationToken.None);
            if (success)
            {
                var info = await _network.SignalR.GetGuildInfoAsync(guildId, CancellationToken.None);
                CurrentGuild = info;
                OnGuildUpdated?.Invoke(info);
            }
            else
            {
                OnGuildMessage?.Invoke("Join guild failed.");
            }
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteGuild] JoinGuild failed: {ex.Message}");
        }
    }

    public async void SendChat(SendChatCommandDto cmd)
    {
        try
        {
            var result = await _network.SignalR.SendChatAsync(cmd, CancellationToken.None);
            if (!result.Success)
                OnGuildMessage?.Invoke($"Send chat failed: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteGuild] SendChat failed: {ex.Message}");
        }
    }

    public async void FetchMail()
    {
        try
        {
            var mail = await _network.SignalR.GetMailAsync(_playerId, CancellationToken.None);
            CachedMail = mail;
            OnMailRefreshed?.Invoke(mail);
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteGuild] FetchMail failed: {ex.Message}");
        }
    }

    public async void SendMail(SendMailCommandDto cmd)
    {
        try
        {
            var result = await _network.SignalR.SendMailAsync(cmd, CancellationToken.None);
            if (!result.Success)
                OnGuildMessage?.Invoke($"Send mail failed: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteGuild] SendMail failed: {ex.Message}");
        }
    }

    // ═══ 服务端事件回调 ═══

    public void HandleGuildCreated(Guid guildId, string guildName, Guid founderId)
        => OnGuildMessage?.Invoke($"Guild '{guildName}' created");

    public void HandleGuildMemberJoined(Guid guildId, Guid playerId)
        => OnGuildMessage?.Invoke("New member joined guild");

    public void HandleMailReceived(MailDto mail)
    {
        OnGuildMessage?.Invoke($"New mail: {mail.Subject}");
        FetchMail();
    }
}
