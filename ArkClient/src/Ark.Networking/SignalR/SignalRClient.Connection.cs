using Game.Shared.Core.DTOs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Ark.Networking.SignalR;

// ── 连接管理 ─────────────────────────────────────────────────────────

public sealed partial class SignalRClient
{
    public async Task ConnectAsync(string hubUrl, CancellationToken ct = default)
    {
        if (_connection is not null)
            await DisposeAsync();

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.Closed += ex =>
        {
            _logger?.LogWarning(ex, "SignalR connection closed");
            OnClosed?.Invoke(ex);
            return Task.CompletedTask;
        };

        _connection.Reconnecting += ex =>
        {
            _logger?.LogInformation("SignalR reconnecting: {Reason}", ex?.Message);
            OnReconnecting?.Invoke(ex?.Message);
            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            _logger?.LogInformation("SignalR reconnected: {ConnectionId}", connectionId);
            OnReconnected?.Invoke(connectionId);
            return Task.CompletedTask;
        };

        if (_eventHandler is not null)
            RegisterEventHandlers(_eventHandler);

        _logger?.LogInformation("Connecting to SignalR hub at {Url}", hubUrl);
        await _connection.StartAsync(ct);
        _logger?.LogInformation("SignalR connected, ConnectionId={ConnectionId}", _connection.ConnectionId);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_connection is not null)
        {
            await _connection.StopAsync(ct);
            _logger?.LogInformation("SignalR disconnected");
        }
    }

    public void SetEventHandler(IServerEventHandler handler)
    {
        _eventHandler = handler;

        if (_connection is not null)
            RegisterEventHandlers(handler);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    private HubConnection Connection =>
        _connection ?? throw new InvalidOperationException("SignalR not connected. Call ConnectAsync first.");
}
