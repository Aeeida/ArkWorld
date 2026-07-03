using Xunit;
using Ark.Abstractions;
using Ark.Events;
using Ark.Gameplay.Life;

namespace Ark.Tests;

/// <summary>测试用瞬时动作。</summary>
file sealed class InstantAction : IGameAction
{
    public string ActionId => "TestInstant";
    public float Duration => 0;
    public bool Executed { get; private set; }
    public bool Completed { get; private set; }

    public bool CanExecute(int entityId) => true;
    public void Execute(int entityId) => Executed = true;
    public void OnComplete(int entityId) => Completed = true;
}

/// <summary>测试用持续动作。</summary>
file sealed class TimedAction : IGameAction
{
    public string ActionId => "TestTimed";
    public float Duration => 2f;
    public bool Completed { get; private set; }

    public bool CanExecute(int entityId) => true;
    public void Execute(int entityId) { }
    public void OnComplete(int entityId) => Completed = true;
}

/// <summary>测试用不可执行动作。</summary>
file sealed class BlockedAction : IGameAction
{
    public string ActionId => "TestBlocked";
    public float Duration => 1f;

    public bool CanExecute(int entityId) => false;
    public void Execute(int entityId) { }
    public void OnComplete(int entityId) { }
}

public class ActionSystemTests
{
    [Fact]
    public void Execute_InstantAction_CompletesImmediately()
    {
        var bus = new EventBus();
        var sys = new ActionSystem(bus);
        var action = new InstantAction();

        bool result = sys.Execute(action, 1);

        Assert.True(result);
        Assert.True(action.Executed);
        Assert.True(action.Completed);
        Assert.False(sys.IsActive(1));
    }

    [Fact]
    public void Execute_TimedAction_BecomesActive()
    {
        var bus = new EventBus();
        var sys = new ActionSystem(bus);
        var action = new TimedAction();

        sys.Execute(action, 1);

        Assert.True(sys.IsActive(1));
        Assert.Equal("TestTimed", sys.GetCurrentActionId(1));
        Assert.False(action.Completed);
    }

    [Fact]
    public void Update_CompletesTimedAction()
    {
        var bus = new EventBus();
        var sys = new ActionSystem(bus);
        var action = new TimedAction();

        sys.Execute(action, 1);
        sys.Update(3f); // > 2s duration

        Assert.True(action.Completed);
        Assert.False(sys.IsActive(1));
    }

    [Fact]
    public void Execute_BlockedAction_ReturnsFalse()
    {
        var bus = new EventBus();
        var sys = new ActionSystem(bus);

        bool result = sys.Execute(new BlockedAction(), 1);

        Assert.False(result);
        Assert.False(sys.IsActive(1));
    }

    [Fact]
    public void Execute_PublishesStartAndCompleteEvents()
    {
        var bus = new EventBus();
        var sys = new ActionSystem(bus);

        ActionStartedEvent? started = null;
        ActionCompletedEvent? completed = null;
        bus.Subscribe<ActionStartedEvent>(e => started = e);
        bus.Subscribe<ActionCompletedEvent>(e => completed = e);

        var action = new TimedAction();
        sys.Execute(action, 42);

        Assert.NotNull(started);
        Assert.Equal("TestTimed", started.Value.ActionId);
        Assert.Equal(42, started.Value.EntityId);

        sys.Update(3f);

        Assert.NotNull(completed);
        Assert.Equal("TestTimed", completed.Value.ActionId);
    }

    [Fact]
    public void Cancel_StopsActiveAction()
    {
        var bus = new EventBus();
        var sys = new ActionSystem(bus);

        sys.Execute(new TimedAction(), 1);
        Assert.True(sys.IsActive(1));

        sys.Cancel(1);
        Assert.False(sys.IsActive(1));
    }
}
