using GameServer.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;
using System.Text.Json;

namespace GameServer.Modules.Scripting.Services;

/// <summary>
/// Watches a configured directory for script JSON files and hot-reloads them into the ScriptManagerGrain.
/// In production: uses FileSystemWatcher + Redis pub/sub for cluster-wide invalidation.
/// </summary>
public sealed class ScriptHotReloadService(
    ILogger<ScriptHotReloadService> logger)
{
    private FileSystemWatcher? _watcher;
    private string _scriptDirectory = string.Empty;

    public Task StartAsync(CancellationToken ct = default)
    {
        _scriptDirectory = Path.Combine(AppContext.BaseDirectory, "Scripts");

        if (!Directory.Exists(_scriptDirectory))
        {
            Directory.CreateDirectory(_scriptDirectory);
            logger.LogInformation("Created script directory: {Dir}", _scriptDirectory);
        }

        _watcher = new FileSystemWatcher(_scriptDirectory, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnScriptFileChanged;
        _watcher.Created += OnScriptFileChanged;

        logger.LogInformation("Script hot-reload watcher started on {Dir}", _scriptDirectory);
        return Task.CompletedTask;
    }

    private void OnScriptFileChanged(object sender, FileSystemEventArgs e)
    {
        logger.LogInformation("Script file changed: {File} ({ChangeType})", e.FullPath, e.ChangeType);
        // In production: parse JSON, validate schema, push to ScriptManagerGrain via IGrainFactory
        // For now, log the event for monitoring
    }

    /// <summary>
    /// Manually load a script definition from a JSON string.
    /// Used by GM tools and automated deployment pipelines.
    /// </summary>
    public static ScriptDefinition? ParseScriptJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ScriptDefinition>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Manages scheduled activities calendar. Checks for upcoming activities and ensures
/// they are properly activated/deactivated via the ScriptManagerGrain.
/// </summary>
public sealed class ActivityCalendarService(
    ILogger<ActivityCalendarService> logger)
{
    public Task StartAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Activity calendar service started");
        // In production: periodic timer to check for activities that need activation
        // Orleans Reminders in ScriptManagerGrain handle the actual scheduling
        return Task.CompletedTask;
    }
}

/// <summary>
/// Records all script execution events for audit and compliance.
/// In production: writes to a dedicated audit log table via EF Core.
/// </summary>
public sealed class ScriptAuditService(
    ILogger<ScriptAuditService> logger)
{
    public void RecordExecution(Guid playerId, string scriptId, string action, string details)
    {
        logger.LogInformation("[AUDIT] Player {PlayerId} | Script {ScriptId} | {Action} | {Details}",
            playerId, scriptId, action, details);
    }

    public void RecordRegistration(string scriptId, int version, string author)
    {
        logger.LogInformation("[AUDIT] Script {ScriptId} v{Version} registered by {Author}",
            scriptId, version, author);
    }

    public void RecordRollback(string scriptId, int fromVersion, int toVersion)
    {
        logger.LogInformation("[AUDIT] Script {ScriptId} rolled back from v{From} to v{To}",
            scriptId, fromVersion, toVersion);
    }
}
