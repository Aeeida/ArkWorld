using Xunit;
using Ark.Configuration;
using Ark.Events;

namespace Ark.Tests.Integration;

public class ConfigEventIntegrationTests
{
    [Fact]
    public void EventBus_WithGameConfig_EndToEnd()
    {
        // Arrange: custom config + event bus
        var config = new GameConfig();
        config.Player.WalkSpeed = 12f;

        var bus = new EventBus();
        float capturedSpeed = 0;

        bus.Subscribe<GameStateChangedEvent>(e =>
        {
            if (e.NewState == "Playing")
                capturedSpeed = config.Player.WalkSpeed;
        });

        // Act
        bus.Publish(new GameStateChangedEvent("Menu", "Playing"));

        // Assert
        Assert.Equal(12f, capturedSpeed);
    }
}
