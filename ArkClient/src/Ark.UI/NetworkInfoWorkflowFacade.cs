using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ark.Services;
using Game.Shared.Core.DTOs;

namespace Ark.UI;

internal readonly record struct LoginWorkflowResult(bool Success, string PlayerName, string ErrorMessage);
internal readonly record struct OperationWorkflowResult(bool Success, string ErrorMessage);
internal readonly record struct SelectCharacterWorkflowResult(bool Success, System.Guid CharacterId, string ErrorMessage);

internal sealed class CharacterSlotViewModel
{
    public System.Guid CharacterId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Level { get; init; }
    public string Faction { get; init; } = string.Empty;
    public string CharacterClass { get; init; } = string.Empty;
    public string LastZone { get; init; } = string.Empty;
}

internal sealed class CharacterListWorkflowResult
{
    public int MaxSlots { get; init; }
    public List<CharacterSlotViewModel> Characters { get; init; } = [];
    public bool IsAvailable { get; init; }
}

internal sealed class NetworkInfoWorkflowFacade
{
    public async Task<LoginWorkflowResult> LoginAsync(string host, int port, string account, string password)
    {
        var result = await GameServices.LoginRemoteAsync(host, port, account, password, "ark-client", "ark-ui");
        if (result?.Success == true && result.Player is not null)
            return new LoginWorkflowResult(true, result.Player.Name, string.Empty);

        return new LoginWorkflowResult(false, string.Empty, result?.ErrorMessage ?? "未知错误");
    }

    public CharacterListWorkflowResult ReadCachedCharacterList()
        => MapCharacterList(GameServices.Character?.CachedCharacterList);

    public async Task<CharacterListWorkflowResult> RefreshCharacterListAsync()
    {
        if (GameServices.Character is null)
            return new CharacterListWorkflowResult { IsAvailable = false };

        var list = await GameServices.Character.FetchCharacterListAsync();
        return MapCharacterList(list);
    }

    public async Task<OperationWorkflowResult> CreateCharacterAsync(
        System.Guid accountId,
        string name,
        string faction,
        string characterClass,
        string startingZone)
    {
        if (GameServices.Character is null)
            return new OperationWorkflowResult(false, "角色服务未初始化");

        var result = await GameServices.Character.CreateCharacterAsync(new CreateCharacterFullCommandDto(
            PlayerId: accountId,
            RequestId: System.Guid.NewGuid(),
            Timestamp: DateTime.UtcNow,
            Name: name,
            Faction: faction,
            CharacterClass: characterClass,
            SquadMemberCount: 0,
            SquadMembers: [],
            Appearance: null,
            StartingZone: startingZone));

        return result?.Success == true
            ? new OperationWorkflowResult(true, string.Empty)
            : new OperationWorkflowResult(false, result?.ErrorMessage ?? "未知错误");
    }

    public async Task<SelectCharacterWorkflowResult> SelectCharacterAsync(System.Guid characterId)
    {
        if (GameServices.Character is null)
            return new SelectCharacterWorkflowResult(false, System.Guid.Empty, "角色服务未初始化");

        var result = await GameServices.Character.SelectCharacterAsync(characterId);
        if (result?.Success == true && result.CharacterId.HasValue)
            return new SelectCharacterWorkflowResult(true, result.CharacterId.Value, string.Empty);

        return new SelectCharacterWorkflowResult(false, System.Guid.Empty, result?.ErrorMessage ?? "未知错误");
    }

    private static CharacterListWorkflowResult MapCharacterList(CharacterListDto? list)
    {
        if (list is null)
            return new CharacterListWorkflowResult { IsAvailable = false };

        var mapped = new CharacterListWorkflowResult
        {
            IsAvailable = true,
            MaxSlots = list.MaxSlots,
            Characters = new List<CharacterSlotViewModel>(list.Characters.Count)
        };

        foreach (var slot in list.Characters)
        {
            mapped.Characters.Add(new CharacterSlotViewModel
            {
                CharacterId = slot.CharacterId,
                Name = slot.Name,
                Level = slot.Level,
                Faction = slot.Faction,
                CharacterClass = slot.CharacterClass,
                LastZone = slot.LastZone ?? "default",
            });
        }

        return mapped;
    }
}
