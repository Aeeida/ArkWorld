using System;
using System.Collections.Generic;
using System.Threading;
using Game.Shared.Core.DTOs;

namespace Ark.Services.Remote;

/// <summary>
/// 远程角色服务 — 管理角色列表、创建（含队友/小队配置）、选择流程。
/// 所有状态来自服务端，客户端仅缓存最新拉取结果。
/// </summary>
public sealed class RemoteCharacterService
{
    private readonly Networking.NetworkManager _network;
    private Guid _accountId;

    public Guid AccountId => _accountId;
    public CharacterListDto? CachedCharacterList { get; private set; }
    public SelectCharacterResultDto? LastSelectionResult { get; private set; }
    public Guid SelectedCharacterId { get; private set; }

    public event Action<CharacterListDto>? OnCharacterListReceived;
    public event Action<CharacterCreateFullResultDto>? OnCharacterCreated;
    public event Action<SelectCharacterResultDto>? OnCharacterSelected;

    public RemoteCharacterService(Networking.NetworkManager network, Guid accountId)
    {
        _network = network;
        _accountId = accountId;
    }

    public void SetAccountId(Guid accountId) => _accountId = accountId;

    /// <summary>从服务端获取角色列表（登录成功后调用）。</summary>
    public async void FetchCharacterList()
        => await FetchCharacterListAsync();

    public async Task<CharacterListDto?> FetchCharacterListAsync()
    {
        try
        {
            var list = await _network.SignalR.GetCharacterListAsync(_accountId, CancellationToken.None);
            CachedCharacterList = list;
            OnCharacterListReceived?.Invoke(list);
            return list;
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteCharacter] FetchCharacterList failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>创建新角色（含队友/小队设置）。</summary>
    public async void CreateCharacter(CreateCharacterFullCommandDto cmd)
        => await CreateCharacterAsync(cmd);

    public async Task<CharacterCreateFullResultDto?> CreateCharacterAsync(CreateCharacterFullCommandDto cmd)
    {
        try
        {
            var result = await _network.SignalR.CreateCharacterFullAsync(cmd, CancellationToken.None);
            OnCharacterCreated?.Invoke(result);
            if (result.Success)
                await FetchCharacterListAsync();
            return result;
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteCharacter] CreateCharacter failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>选择已有角色进入游戏。</summary>
    public async void SelectCharacter(Guid characterId)
        => await SelectCharacterAsync(characterId);

    public async Task<SelectCharacterResultDto?> SelectCharacterAsync(Guid characterId)
    {
        try
        {
            var cmd = new SelectCharacterCommandDto(
                _accountId, Guid.NewGuid(), DateTime.UtcNow, characterId);
            var result = await _network.SignalR.SelectCharacterAsync(cmd, CancellationToken.None);
            LastSelectionResult = result;
            if (result.Success && result.CharacterId.HasValue)
                SelectedCharacterId = result.CharacterId.Value;
            OnCharacterSelected?.Invoke(result);
            return result;
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteCharacter] SelectCharacter failed: {ex.Message}");
            return null;
        }
    }

    // ═══ 服务端事件回调 ═══

    public void HandleCharacterListReceived(CharacterListDto list)
    {
        CachedCharacterList = list;
        OnCharacterListReceived?.Invoke(list);
    }

    public void HandleCharacterCreated(CharacterCreateFullResultDto result)
    {
        OnCharacterCreated?.Invoke(result);
    }

    public void HandleCharacterSelected(SelectCharacterResultDto result)
    {
        LastSelectionResult = result;
        if (result.Success && result.CharacterId.HasValue)
            SelectedCharacterId = result.CharacterId.Value;
        OnCharacterSelected?.Invoke(result);
    }
}
