using FluentAssertions;
using GameServer.Grains.Interfaces;
using GameServer.Tests.Integration.Infrastructure;

namespace GameServer.Tests.Integration.Grains;

public class ScriptManagerGrainTests : GrainTestBase
{
    private static ScriptDefinition CreateSimpleScript(string scriptId = "test-script", string author = "tester") => new()
    {
        ScriptId = scriptId,
        Name = "Test Script",
        Description = "A test script",
        Category = "Quest",
        Author = author,
        IsActive = true,
        EntryNodeId = "node-1",
        Nodes =
        [
            new ScriptNode
            {
                NodeId = "node-1",
                Type = "Dialogue",
                Properties = new() { ["speaker"] = "NPC", ["text"] = "Hello!" },
                DialogueOptions =
                [
                    new DialogueOption { Index = 0, Text = "Hi", TargetNodeId = "node-2" },
                    new DialogueOption { Index = 1, Text = "Bye", TargetNodeId = "node-end" }
                ]
            },
            new ScriptNode
            {
                NodeId = "node-2",
                Type = "Action",
                Properties = new() { ["action_type"] = "SetVariable", ["action_param"] = "greeted", ["action_value"] = "true" },
                Transitions = [new ScriptTransition { TargetNodeId = "node-end", IsDefault = true }]
            },
            new ScriptNode { NodeId = "node-end", Type = "End" }
        ]
    };

    [Fact]
    public async Task RegisterScript_NewScript_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptManagerGrain>("global");

        var result = await grain.RegisterScriptAsync(CreateSimpleScript("register-1"));

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterScript_ShouldSetVersionToOne()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptManagerGrain>("global");
        await grain.RegisterScriptAsync(CreateSimpleScript("register-ver"));

        var script = await grain.GetScriptAsync("register-ver");

        script.Should().NotBeNull();
        script!.Version.Should().Be(1);
    }

    [Fact]
    public async Task RegisterScript_Duplicate_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptManagerGrain>("global");
        await grain.RegisterScriptAsync(CreateSimpleScript("register-dup"));

        var result = await grain.RegisterScriptAsync(CreateSimpleScript("register-dup"));

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetScript_Existing_ShouldReturn()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptManagerGrain>("global");
        await grain.RegisterScriptAsync(CreateSimpleScript("get-script-1"));

        var script = await grain.GetScriptAsync("get-script-1");

        script.Should().NotBeNull();
        script!.ScriptId.Should().Be("get-script-1");
        script.Name.Should().Be("Test Script");
    }

    [Fact]
    public async Task GetScript_NonExistent_ShouldReturnNull()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptManagerGrain>("global");

        var script = await grain.GetScriptAsync("does-not-exist");

        script.Should().BeNull();
    }

    [Fact]
    public async Task UpdateScript_Existing_ShouldIncrementVersion()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptManagerGrain>("global");
        await grain.RegisterScriptAsync(CreateSimpleScript("update-1"));

        var updated = CreateSimpleScript("update-1");
        updated.Name = "Updated Script";
        var result = await grain.UpdateScriptAsync(updated);

        result.Should().BeTrue();
        var script = await grain.GetScriptAsync("update-1");
        script!.Version.Should().Be(2);
        script.Name.Should().Be("Updated Script");
    }

    [Fact]
    public async Task UpdateScript_NonExistent_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptManagerGrain>("global");

        var result = await grain.UpdateScriptAsync(CreateSimpleScript("no-exist"));

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateScript_MultipleUpdates_ShouldTrackVersionHistory()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptManagerGrain>("global");
        await grain.RegisterScriptAsync(CreateSimpleScript("version-history"));

        var v2 = CreateSimpleScript("version-history");
        v2.Name = "V2";
        await grain.UpdateScriptAsync(v2);

        var v3 = CreateSimpleScript("version-history");
        v3.Name = "V3";
        await grain.UpdateScriptAsync(v3);

        var script = await grain.GetScriptAsync("version-history");
        script!.Version.Should().Be(3);

        var state = await grain.GetStateAsync();
        state.VersionHistory["version-history"].Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllScripts_ShouldReturnAll()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptManagerGrain>("global");
        await grain.RegisterScriptAsync(CreateSimpleScript("all-1"));
        await grain.RegisterScriptAsync(CreateSimpleScript("all-2"));
        await grain.RegisterScriptAsync(CreateSimpleScript("all-3"));

        var all = await grain.GetAllScriptsAsync();

        all.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task ScheduleActivity_ValidScript_ShouldReturnActivityId()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptManagerGrain>("global");
        await grain.RegisterScriptAsync(CreateSimpleScript("activity-script"));

        var activityId = await grain.ScheduleActivityAsync(
            "activity-script",
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            "zone-1");

        activityId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ScheduleActivity_NonExistentScript_ShouldReturnEmpty()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptManagerGrain>("global");

        var activityId = await grain.ScheduleActivityAsync(
            "non-existent",
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            null);

        activityId.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task CancelActivity_Existing_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptManagerGrain>("global");
        await grain.RegisterScriptAsync(CreateSimpleScript("cancel-act-script"));
        var activityId = await grain.ScheduleActivityAsync(
            "cancel-act-script",
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            null);

        var result = await grain.CancelActivityAsync(activityId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CancelActivity_NonExistent_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptManagerGrain>("global");

        var result = await grain.CancelActivityAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetActiveActivities_ShouldFilterCancelled()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptManagerGrain>("global");
        await grain.RegisterScriptAsync(CreateSimpleScript("filter-act-script"));

        var actId1 = await grain.ScheduleActivityAsync("filter-act-script",
            DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(3), null);
        await grain.ScheduleActivityAsync("filter-act-script",
            DateTime.UtcNow.AddHours(2), DateTime.UtcNow.AddHours(4), null);

        await grain.CancelActivityAsync(actId1);

        var active = await grain.GetActiveActivitiesAsync();
        active.Should().HaveCount(1);
    }

    [Fact]
    public async Task RollbackScript_ToValidVersion_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptManagerGrain>("global");
        var original = CreateSimpleScript("rollback-script");
        original.Name = "Original";
        await grain.RegisterScriptAsync(original);

        var v2 = CreateSimpleScript("rollback-script");
        v2.Name = "Updated";
        await grain.UpdateScriptAsync(v2);

        var result = await grain.RollbackScriptAsync("rollback-script", 1);

        result.Should().BeTrue();
        var script = await grain.GetScriptAsync("rollback-script");
        script!.Name.Should().Be("Original");
    }

    [Fact]
    public async Task RollbackScript_ToInvalidVersion_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptManagerGrain>("global");
        await grain.RegisterScriptAsync(CreateSimpleScript("rollback-invalid"));

        var result = await grain.RollbackScriptAsync("rollback-invalid", 99);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RollbackScript_NonExistentScript_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptManagerGrain>("global");

        var result = await grain.RollbackScriptAsync("not-exist", 1);

        result.Should().BeFalse();
    }
}
