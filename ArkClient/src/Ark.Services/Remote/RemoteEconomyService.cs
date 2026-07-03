using System;
using System.Collections.Generic;
using System.Threading;
using Game.Shared.Core.DTOs;

namespace Ark.Services.Remote;

/// <summary>
/// 远程经济服务 — 市场/交易/制造，所有操作通过 SignalR RPC 发送到服务端。
/// </summary>
public sealed class RemoteEconomyService
{
    private readonly Networking.NetworkManager _network;
    private readonly Guid _playerId;

    public MarketOrdersDto? CachedMarketOrders { get; private set; }
    public CraftingQueueDto? CachedCraftingQueue { get; private set; }

    public event Action<MarketOrdersDto>? OnMarketOrdersRefreshed;
    public event Action<CraftingQueueDto>? OnCraftingQueueRefreshed;
    public event Action<string>? OnEconomyMessage;

    public RemoteEconomyService(Networking.NetworkManager network, Guid playerId)
    {
        _network = network;
        _playerId = playerId;
    }

    public async void FetchMarketOrders(string stationId)
    {
        try
        {
            var orders = await _network.SignalR.GetMarketOrdersAsync(stationId, CancellationToken.None);
            CachedMarketOrders = orders;
            OnMarketOrdersRefreshed?.Invoke(orders);
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteEconomy] FetchMarketOrders failed: {ex.Message}");
        }
    }

    public async void PlaceOrder(CreateMarketOrderCommandDto cmd)
    {
        try
        {
            var result = await _network.SignalR.CreateMarketOrderAsync(cmd, CancellationToken.None);
            if (result.Success)
                OnEconomyMessage?.Invoke("Market order placed successfully.");
            else
                OnEconomyMessage?.Invoke($"Order failed: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteEconomy] PlaceOrder failed: {ex.Message}");
        }
    }

    public async void BuyOrder(BuyOrderCommandDto cmd)
    {
        try
        {
            var result = await _network.SignalR.BuyOrderAsync(cmd, CancellationToken.None);
            if (result.Success)
                OnEconomyMessage?.Invoke($"Bought {result.QuantityFilled} items for {result.TotalCost}.");
            else
                OnEconomyMessage?.Invoke($"Buy failed: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteEconomy] BuyOrder failed: {ex.Message}");
        }
    }

    public async void StartCrafting(StartCraftCommandDto cmd)
    {
        try
        {
            var result = await _network.SignalR.StartCraftingAsync(cmd, CancellationToken.None);
            if (result.Success)
                OnEconomyMessage?.Invoke("Crafting started.");
            else
                OnEconomyMessage?.Invoke($"Crafting failed: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteEconomy] StartCrafting failed: {ex.Message}");
        }
    }

    public async void FetchCraftingQueue()
    {
        try
        {
            var queue = await _network.SignalR.GetCraftingQueueAsync(_playerId, CancellationToken.None);
            CachedCraftingQueue = queue;
            OnCraftingQueueRefreshed?.Invoke(queue);
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteEconomy] FetchCraftingQueue failed: {ex.Message}");
        }
    }

    // ═══ 服务端事件回调 ═══
    public void HandleCraftingStarted(string blueprintId, int quantity)
        => OnEconomyMessage?.Invoke($"Crafting started: {blueprintId} x{quantity}");

    public void HandleCraftingCompleted(string blueprintId, int quantity)
    {
        OnEconomyMessage?.Invoke($"Crafting completed: {blueprintId} x{quantity}");
        FetchCraftingQueue();
    }

    public void HandleMarketOrderPlaced(Guid orderId, string itemId, int quantity)
        => OnEconomyMessage?.Invoke($"Market order placed: {itemId} x{quantity}");
}
