using Xunit;
using Ark.Configuration;
using Ark.Events;
using Ark.Gameplay.Life;
using Ark.Shared.Data;

namespace Ark.Tests;

public class NeedSystemTests
{
    private static EventBus CreateBus() => new();

    [Fact]
    public void CharacterNeedState_InitialValues_AllMax()
    {
        var state = new CharacterNeedState(1);
        Assert.Equal(100f, state.Get(NeedCategory.Hunger));
        Assert.Equal(100f, state.Get(NeedCategory.Energy));
        Assert.Equal(100f, state.Get(NeedCategory.Fun));
    }

    [Fact]
    public void Modify_ClampsToZero()
    {
        var state = new CharacterNeedState(1);
        state.Modify(NeedCategory.Hunger, -200f);
        Assert.Equal(0f, state.Get(NeedCategory.Hunger));
    }

    [Fact]
    public void Modify_ClampsToMax()
    {
        var state = new CharacterNeedState(1);
        state.Modify(NeedCategory.Hunger, 50f);
        Assert.Equal(100f, state.Get(NeedCategory.Hunger));
    }

    [Fact]
    public void CalculateMood_IsAverageOfNeeds()
    {
        var state = new CharacterNeedState(1);
        // All at 100 → mood = 100
        Assert.Equal(100f, state.CalculateMood(), 0.01f);

        state.Set(NeedCategory.Hunger, 0);
        // 5×100 + 0 = 500 / 6 ≈ 83.33
        Assert.Equal(500f / 6f, state.CalculateMood(), 0.01f);
    }

    [Fact]
    public void NeedSystem_Update_DecaysNeeds()
    {
        var bus = CreateBus();
        var system = new NeedSystem(bus);
        var state = new CharacterNeedState(1);
        system.Register(state);

        float before = state.Get(NeedCategory.Hunger);
        system.Update(1f); // 1 second
        float after = state.Get(NeedCategory.Hunger);

        Assert.True(after < before);
    }

    [Fact]
    public void NeedSystem_PublishesCriticalEvent()
    {
        var bus = CreateBus();
        var system = new NeedSystem(bus);
        var state = new CharacterNeedState(1);
        system.Register(state);

        // Set just above threshold
        float threshold = GameConfig.Current.Life.NeedCriticalThreshold;
        state.Set(NeedCategory.Energy, threshold + 0.1f);

        NeedCriticalEvent? received = null;
        bus.Subscribe<NeedCriticalEvent>(e => received = e);

        // Decay enough to cross threshold
        system.Update(1f);

        Assert.NotNull(received);
        Assert.Equal("Energy", received.Value.NeedId);
    }

    [Fact]
    public void NeedSystem_TimeScale_AcceleratesDecay()
    {
        var bus = CreateBus();
        var system = new NeedSystem(bus);
        var s1 = new CharacterNeedState(1);
        var s2 = new CharacterNeedState(2);
        system.Register(s1);
        system.Register(s2);

        system.Update(1f, 1f); // normal for s1
        float v1 = s1.Get(NeedCategory.Hunger);

        // Reset s2 and update with 2x scale
        system.Unregister(s1);
        system.Update(1f, 2f);
        float v2 = s2.Get(NeedCategory.Hunger);

        // s2 should have decayed more (2x rate for same wall-clock time)
        Assert.True(v2 < v1);
    }
}
