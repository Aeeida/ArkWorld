using FluentAssertions;
using GameServer.Grains.Interfaces;
using GameServer.Tests.Integration.Infrastructure;

namespace GameServer.Tests.Integration.Grains;

public class ScriptInstanceGrainTests : GrainTestBase
{
    private async Task RegisterDialogueScript(string scriptId = "dialogue-script")
    {
        var manager = Cluster.GrainFactory.GetGrain<IScriptManagerGrain>("global");
        await manager.RegisterScriptAsync(new ScriptDefinition
        {
            ScriptId = scriptId,
            Name = "Dialogue Script",
            Author = "tester",
            IsActive = true,
            EntryNodeId = "dialogue-1",
            Nodes =
            [
                new ScriptNode
                {
                    NodeId = "dialogue-1",
                    Type = "Dialogue",
                    Properties = new() { ["speaker"] = "Guard", ["text"] = "Halt! Who goes there?" },
                    DialogueOptions =
                    [
                        new DialogueOption { Index = 0, Text = "A friend", TargetNodeId = "action-1" },
                        new DialogueOption { Index = 1, Text = "None of your business", TargetNodeId = "end-node" }
                    ]
                },
                new ScriptNode
                {
                    NodeId = "action-1",
                    Type = "Action",
                    Properties = new() { ["action_type"] = "SetVariable", ["action_param"] = "friendly", ["action_value"] = "true" },
                    Transitions = [new ScriptTransition { TargetNodeId = "end-node", IsDefault = true }]
                },
                new ScriptNode { NodeId = "end-node", Type = "End" }
            ]
        });
    }

    private async Task RegisterActionScript(string scriptId = "action-script")
    {
        var manager = Cluster.GrainFactory.GetGrain<IScriptManagerGrain>("global");
        await manager.RegisterScriptAsync(new ScriptDefinition
        {
            ScriptId = scriptId,
            Name = "Action Script",
            Author = "tester",
            IsActive = true,
            EntryNodeId = "act-1",
            Nodes =
            [
                new ScriptNode
                {
                    NodeId = "act-1",
                    Type = "Action",
                    Properties = new() { ["action_type"] = "SetVariable", ["action_param"] = "started", ["action_value"] = "yes" },
                    Transitions = [new ScriptTransition { TargetNodeId = "act-2", IsDefault = true }]
                },
                new ScriptNode
                {
                    NodeId = "act-2",
                    Type = "Action",
                    Properties = new() { ["action_type"] = "SetVariable", ["action_param"] = "step2", ["action_value"] = "done" },
                    Transitions = [new ScriptTransition { TargetNodeId = "end", IsDefault = true }]
                },
                new ScriptNode { NodeId = "end", Type = "End" }
            ]
        });
    }

    [Fact]
    public async Task StartScript_ValidScript_ShouldSucceed()
    {
        await RegisterDialogueScript("start-valid");
        var grain = Cluster.GrainFactory.GetGrain<IScriptInstanceGrain>(Guid.NewGuid());

        var result = await grain.StartScriptAsync("start-valid", 0);

        result.Should().BeTrue();
        var ids = await grain.GetActiveScriptIdsAsync();
        ids.Should().Contain("start-valid");
    }

    [Fact]
    public async Task StartScript_AlreadyRunning_ShouldReturnFalse()
    {
        await RegisterDialogueScript("start-dup");
        var grain = Cluster.GrainFactory.GetGrain<IScriptInstanceGrain>(Guid.NewGuid());
        await grain.StartScriptAsync("start-dup", 0);

        var result = await grain.StartScriptAsync("start-dup", 0);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task StartScript_NonExistent_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptInstanceGrain>(Guid.NewGuid());

        var result = await grain.StartScriptAsync("not-registered", 0);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task StartScript_DialogueNode_ShouldPauseAtDialogue()
    {
        await RegisterDialogueScript("pause-dialogue");
        var grain = Cluster.GrainFactory.GetGrain<IScriptInstanceGrain>(Guid.NewGuid());

        await grain.StartScriptAsync("pause-dialogue", 0);

        var state = await grain.GetStateAsync();
        state.ActiveScripts.Should().ContainKey("pause-dialogue");
        state.ActiveScripts["pause-dialogue"].Status.Should().Be("WaitingDialogue");
    }

    [Fact]
    public async Task StartScript_ActionOnlyScript_ShouldAutoComplete()
    {
        await RegisterActionScript("auto-complete");
        var grain = Cluster.GrainFactory.GetGrain<IScriptInstanceGrain>(Guid.NewGuid());

        await grain.StartScriptAsync("auto-complete", 0);

        var state = await grain.GetStateAsync();
        state.ActiveScripts.Should().NotContainKey("auto-complete");
        state.CompletedScriptIds.Should().Contain("auto-complete");
    }

    [Fact]
    public async Task GetDialogue_AtDialogueNode_ShouldReturnSnapshot()
    {
        await RegisterDialogueScript("get-dlg");
        var grain = Cluster.GrainFactory.GetGrain<IScriptInstanceGrain>(Guid.NewGuid());
        await grain.StartScriptAsync("get-dlg", 0);

        var dialogue = await grain.GetDialogueAsync("get-dlg");

        dialogue.ScriptId.Should().Be("get-dlg");
        dialogue.SpeakerName.Should().Be("Guard");
        dialogue.Text.Should().Be("Halt! Who goes there?");
        dialogue.Options.Should().HaveCount(2);
        dialogue.Options[0].Text.Should().Be("A friend");
        dialogue.Options[1].Text.Should().Be("None of your business");
    }

    [Fact]
    public async Task GetDialogue_NoActiveScript_ShouldReturnFallback()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptInstanceGrain>(Guid.NewGuid());

        var dialogue = await grain.GetDialogueAsync("not-started");

        dialogue.Text.Should().Be("No active script.");
    }

    [Fact]
    public async Task ChooseDialogueOption_ValidOption_ShouldAdvance()
    {
        await RegisterDialogueScript("choose-opt");
        var grain = Cluster.GrainFactory.GetGrain<IScriptInstanceGrain>(Guid.NewGuid());
        await grain.StartScriptAsync("choose-opt", 0);

        // Choose "A friend" which leads to action-1 then end
        var result = await grain.ChooseDialogueOptionAsync("choose-opt", 0);

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        // After choosing, the action node runs and then script completes
        state.CompletedScriptIds.Should().Contain("choose-opt");
    }

    [Fact]
    public async Task ChooseDialogueOption_ByeOption_ShouldComplete()
    {
        await RegisterDialogueScript("choose-bye");
        var grain = Cluster.GrainFactory.GetGrain<IScriptInstanceGrain>(Guid.NewGuid());
        await grain.StartScriptAsync("choose-bye", 0);

        // Choose "None of your business" which leads to end-node
        var result = await grain.ChooseDialogueOptionAsync("choose-bye", 1);

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.CompletedScriptIds.Should().Contain("choose-bye");
    }

    [Fact]
    public async Task ChooseDialogueOption_InvalidScript_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptInstanceGrain>(Guid.NewGuid());

        var result = await grain.ChooseDialogueOptionAsync("not-exist", 0);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AbortScript_ActiveScript_ShouldSucceed()
    {
        await RegisterDialogueScript("abort-script");
        var grain = Cluster.GrainFactory.GetGrain<IScriptInstanceGrain>(Guid.NewGuid());
        await grain.StartScriptAsync("abort-script", 0);

        var result = await grain.AbortScriptAsync("abort-script");

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.ActiveScripts.Should().NotContainKey("abort-script");
    }

    [Fact]
    public async Task AbortScript_NotActive_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptInstanceGrain>(Guid.NewGuid());

        var result = await grain.AbortScriptAsync("not-started");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetActiveScriptIds_ShouldReturnRunningScripts()
    {
        await RegisterDialogueScript("active-ids-1");
        await RegisterDialogueScript("active-ids-2");
        var grain = Cluster.GrainFactory.GetGrain<IScriptInstanceGrain>(Guid.NewGuid());
        await grain.StartScriptAsync("active-ids-1", 0);
        await grain.StartScriptAsync("active-ids-2", 0);

        var ids = await grain.GetActiveScriptIdsAsync();

        ids.Should().Contain("active-ids-1");
        ids.Should().Contain("active-ids-2");
    }

    [Fact]
    public async Task MultipleScripts_ShouldRunIndependently()
    {
        await RegisterDialogueScript("multi-1");
        await RegisterDialogueScript("multi-2");
        var grain = Cluster.GrainFactory.GetGrain<IScriptInstanceGrain>(Guid.NewGuid());

        await grain.StartScriptAsync("multi-1", 0);
        await grain.StartScriptAsync("multi-2", 0);

        // Abort one
        await grain.AbortScriptAsync("multi-1");

        var ids = await grain.GetActiveScriptIdsAsync();
        ids.Should().Contain("multi-2");
        ids.Should().NotContain("multi-1");
    }

    [Fact]
    public async Task Advance_NoActiveScript_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<IScriptInstanceGrain>(Guid.NewGuid());

        var result = await grain.AdvanceAsync("not-started");

        result.Should().BeFalse();
    }
}
