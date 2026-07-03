using Game.Shared.Core.DTOs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Ark.Networking.SignalR;

/// <summary>
/// SignalR 客户端 — 封装 HubConnection，提供类型化的 RPC 调用和服务端事件订阅。
/// 所有业务调用通过 <see cref="IGameServerHubClient"/> 接口暴露，
/// 服务端推送通过 <see cref="IServerEventHandler"/> 接口回调。
/// </summary>
public sealed partial class SignalRClient : IGameServerHubClient, IAsyncDisposable
{
    private HubConnection? _connection;
    private IServerEventHandler? _eventHandler;
    private readonly ILogger<SignalRClient>? _logger;
    private readonly List<IDisposable> _subscriptions = [];

    public SignalRClient(ILogger<SignalRClient>? logger = null)
    {
        _logger = logger;
    }

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public HubConnectionState State => _connection?.State ?? HubConnectionState.Disconnected;

    public event Action<Exception?>? OnClosed;
    public event Action<string?>? OnReconnecting;
    public event Action<string?>? OnReconnected;
}
