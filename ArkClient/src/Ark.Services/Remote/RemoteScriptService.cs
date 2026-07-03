using System;
using System.Collections.Generic;
using System.Threading;
using Game.Shared.Core.DTOs;

namespace Ark.Services.Remote;

/// <summary>
/// 远程脚本/叙事服务 — 管理脚本执行、对话选择、活动参与，所有操作通过 SignalR RPC。
/// </summary>
public sealed class RemoteScriptService
{
    private readonly Networking.NetworkManager _network;
    private readonly Guid _playerId;

    public DialogueDto? CurrentDialogue { get; private set; }
    public IReadOnlyList<ScriptStatusDto>? ActiveScripts { get; private set; }

    public event Action<DialogueDto>? OnDialogueUpdated;
    public event Action<string, int>? OnScriptStarted;
    public event Action<string>? OnScriptCompleted;
    public event Action<string>? OnScriptMessage;

    public RemoteScriptService(Networking.NetworkManager network, Guid playerId)
    {
        _network = network;
        _playerId = playerId;
    }

    public async void StartScript(StartScriptCommandDto cmd)
    {
        try
        {
            var result = await _network.SignalR.StartScriptedNarrativeAsync(cmd, CancellationToken.None);
            OnScriptMessage?.Invoke(result.Success
                ? "Script started."
                : $"Script failed: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteScript] StartScript failed: {ex.Message}");
        }
    }

    public async void ChooseDialogueOption(ChooseDialogueCommandDto cmd)
    {
        try
        {
            var result = await _network.SignalR.ChooseDialogueOptionAsync(cmd, CancellationToken.None);
            OnScriptMessage?.Invoke(result.Success
                ? "Choice made."
                : $"Choice failed: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteScript] ChooseDialogue failed: {ex.Message}");
        }
    }

    public async void TriggerActivity(TriggerActivityCommandDto cmd)
    {
        try
        {
            var result = await _network.SignalR.TriggerWorldActivityAsync(cmd, CancellationToken.None);
            OnScriptMessage?.Invoke(result.Success
                ? "Activity triggered."
                : $"Activity failed: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteScript] TriggerActivity failed: {ex.Message}");
        }
    }

    public async void FetchActiveScripts()
    {
        try
        {
            ActiveScripts = await _network.SignalR.GetActiveScriptsAsync(_playerId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteScript] FetchActiveScripts failed: {ex.Message}");
        }
    }

    public async void FetchDialogue(string scriptId)
    {
        try
        {
            CurrentDialogue = await _network.SignalR.GetDialogueAsync(_playerId, scriptId, CancellationToken.None);
            if (CurrentDialogue is not null)
                OnDialogueUpdated?.Invoke(CurrentDialogue);
        }
        catch (Exception ex)
        {
            ServiceLog.Error($"[RemoteScript] FetchDialogue failed: {ex.Message}");
        }
    }

    // ═══ 服务端事件回调 ═══

    public void HandleScriptStarted(string scriptId, int version)
    {
        OnScriptStarted?.Invoke(scriptId, version);
        FetchActiveScripts();
    }

    public void HandleScriptCompleted(string scriptId)
    {
        OnScriptCompleted?.Invoke(scriptId);
        FetchActiveScripts();
    }

    public void HandleDialogueUpdated(DialogueDto dialogue)
    {
        CurrentDialogue = dialogue;
        OnDialogueUpdated?.Invoke(dialogue);
    }

    public void HandleActivityStarted(Guid activityId, string scriptId)
        => OnScriptMessage?.Invoke($"Activity started: {scriptId}");

    public void HandleActivityEnded(Guid activityId, string scriptId)
        => OnScriptMessage?.Invoke($"Activity ended: {scriptId}");
}
