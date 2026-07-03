using Xunit;

namespace Ark.Analyzers.Tests;

public class BridgeBusinessComponentWriteAnalyzerTests
{
    private const string BridgeAssembly = "Ark.Bridge.TestSubject";

    private const string BusinessComponent = """
namespace Ark.Game.Components;
public struct Building { public int Level; }
""";

    private const string PresentationComponent = """
using Ark.Analyzers.Attributes;
namespace Ark.Game.Components;
[BridgeWritableComponent]
public struct PresentationFeedback { public float Intensity; }
""";

    [Fact]
    public void Reports_AddComponent_On_Business_Component_From_Bridge()
    {
        var src = """
using Friflo.Engine.ECS;
using Ark.Game.Components;
namespace Ark.Bridge.Demo;
public class Writer
{
    public void Run(Entity e) => e.AddComponent(new Building { Level = 2 });
}
""";
        var diags = AnalyzerTestHarness.Run(
            new BridgeBusinessComponentWriteAnalyzer(),
            BridgeAssembly,
            src, BusinessComponent);
        Assert.Single(diags);
        Assert.Equal("ECS005", diags[0].Id);
    }

    [Fact]
    public void Allows_Write_To_BridgeWritableComponent()
    {
        var src = """
using Friflo.Engine.ECS;
using Ark.Game.Components;
namespace Ark.Bridge.Demo;
public class Writer
{
    public void Run(Entity e) => e.AddComponent(new PresentationFeedback { Intensity = 1f });
}
""";
        var diags = AnalyzerTestHarness.Run(
            new BridgeBusinessComponentWriteAnalyzer(),
            BridgeAssembly,
            src, PresentationComponent);
        Assert.Empty(diags);
    }

    [Fact]
    public void Allows_Write_When_Class_Is_EcsAuthorityBridge()
    {
        var src = """
using Friflo.Engine.ECS;
using Ark.Analyzers.Attributes;
using Ark.Game.Components;
namespace Ark.Bridge.Demo;
[EcsAuthorityBridge]
public class Authority
{
    public void Run(Entity e) => e.AddComponent(new Building { Level = 2 });
}
""";
        var diags = AnalyzerTestHarness.Run(
            new BridgeBusinessComponentWriteAnalyzer(),
            BridgeAssembly,
            src, BusinessComponent);
        Assert.Empty(diags);
    }

    [Fact]
    public void Reports_GetComponent_LValue_Assignment()
    {
        var src = """
using Friflo.Engine.ECS;
using Ark.Game.Components;
namespace Ark.Bridge.Demo;
public class Writer
{
    public void Run(Entity e) => e.GetComponent<Building>() = new Building { Level = 9 };
}
""";
        var diags = AnalyzerTestHarness.Run(
            new BridgeBusinessComponentWriteAnalyzer(),
            BridgeAssembly,
            src, BusinessComponent);
        Assert.Single(diags);
        Assert.Equal("ECS005", diags[0].Id);
    }

    [Fact]
    public void Skips_NonBridge_Assemblies()
    {
        var src = """
using Friflo.Engine.ECS;
using Ark.Game.Components;
namespace Ark.Game.Systems;
public class System1
{
    public void Run(Entity e) => e.AddComponent(new Building { Level = 2 });
}
""";
        var diags = AnalyzerTestHarness.Run(
            new BridgeBusinessComponentWriteAnalyzer(),
            "Ark.Gameplay.Combat",
            src, BusinessComponent);
        Assert.Empty(diags);
    }
}
