using Xunit;
using Ark.Configuration;
using Ark.Events;

namespace Ark.Tests;

public class GameConfigTests
{
    [Fact]
    public void DefaultConfig_HasExpectedPlayerValues()
    {
        var cfg = new GameConfig();
        Assert.Equal(5f, cfg.Player.WalkSpeed);
        Assert.Equal(8f, cfg.Player.SprintSpeed);
        Assert.Equal(20f, cfg.Player.Gravity);
        Assert.Equal(2, cfg.Player.MaxJumps);
    }

    [Fact]
    public void Current_CanBeOverridden()
    {
        var original = GameConfig.Current;
        try
        {
            var custom = new GameConfig();
            custom.Player.WalkSpeed = 99f;
            GameConfig.Current = custom;
            Assert.Equal(99f, GameConfig.Current.Player.WalkSpeed);
        }
        finally
        {
            GameConfig.Current = original;
        }
    }
}

public class EventBusTests
{
    [Fact]
    public void Publish_DeliversToSubscriber()
    {
        var bus = new EventBus();
        string? received = null;
        bus.Subscribe<EntitySpawnedEvent>(e => received = e.EntityType);
        bus.Publish(new EntitySpawnedEvent(1, "Tank"));
        Assert.Equal("Tank", received);
    }

    [Fact]
    public void Unsubscribe_StopsDelivery()
    {
        var bus = new EventBus();
        int count = 0;
        var token = bus.Subscribe<EntityDestroyedEvent>(_ => count++);
        bus.Publish(new EntityDestroyedEvent(1));
        token.Dispose();
        bus.Publish(new EntityDestroyedEvent(2));
        Assert.Equal(1, count);
    }

    [Fact]
    public void ClearAll_RemovesAllSubscriptions()
    {
        var bus = new EventBus();
        int count = 0;
        bus.Subscribe<EntitySpawnedEvent>(_ => count++);
        bus.ClearAll();
        bus.Publish(new EntitySpawnedEvent(1, "X"));
        Assert.Equal(0, count);
    }
}
