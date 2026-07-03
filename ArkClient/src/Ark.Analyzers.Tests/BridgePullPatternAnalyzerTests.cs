using Xunit;

namespace Ark.Analyzers.Tests;

public class BridgePullPatternAnalyzerTests
{
    private const string BridgeAssembly = "Ark.Bridge.TestSubject";

    private const string Components = """
namespace Ark.Game.Components;
public struct A { public int V; }
public struct B { public int V; }
public struct C { public int V; }
public struct D { public int V; }
public struct E { public int V; }
public struct F { public int V; }
""";

    [Fact]
    public void Reports_When_Pulls_Reach_Threshold()
    {
        var src = """
using Friflo.Engine.ECS;
using Ark.Game.Components;
namespace Ark.Bridge.Demo;
public class HotMethod
{
    public void Run(Entity e)
    {
        e.TryGetComponent<A>(out _);
        e.TryGetComponent<B>(out _);
        e.TryGetComponent<C>(out _);
        e.TryGetComponent<D>(out _);
        e.TryGetComponent<E>(out _);
    }
}
""";
        var diags = AnalyzerTestHarness.Run(
            new BridgePullPatternAnalyzer(),
            BridgeAssembly,
            src, Components);
        Assert.Single(diags);
        Assert.Equal("ECS007", diags[0].Id);
    }

    [Fact]
    public void Does_Not_Report_Below_Threshold()
    {
        var src = """
using Friflo.Engine.ECS;
using Ark.Game.Components;
namespace Ark.Bridge.Demo;
public class CoolMethod
{
    public void Run(Entity e)
    {
        e.TryGetComponent<A>(out _);
        e.TryGetComponent<B>(out _);
        e.TryGetComponent<C>(out _);
        e.TryGetComponent<D>(out _);
    }
}
""";
        var diags = AnalyzerTestHarness.Run(
            new BridgePullPatternAnalyzer(),
            BridgeAssembly,
            src, Components);
        Assert.Empty(diags);
    }

    [Fact]
    public void Skips_EcsAuthorityBridge_Classes()
    {
        var src = """
using Friflo.Engine.ECS;
using Ark.Analyzers.Attributes;
using Ark.Game.Components;
namespace Ark.Bridge.Demo;
[EcsAuthorityBridge]
public class HotMethod
{
    public void Run(Entity e)
    {
        e.TryGetComponent<A>(out _);
        e.TryGetComponent<B>(out _);
        e.TryGetComponent<C>(out _);
        e.TryGetComponent<D>(out _);
        e.TryGetComponent<E>(out _);
    }
}
""";
        var diags = AnalyzerTestHarness.Run(
            new BridgePullPatternAnalyzer(),
            BridgeAssembly,
            src, Components);
        Assert.Empty(diags);
    }

    [Fact]
    public void Skips_NonBridge_Assemblies()
    {
        var src = """
using Friflo.Engine.ECS;
using Ark.Game.Components;
namespace Ark.Systems.Combat;
public class HotSystem
{
    public void Run(Entity e)
    {
        e.TryGetComponent<A>(out _);
        e.TryGetComponent<B>(out _);
        e.TryGetComponent<C>(out _);
        e.TryGetComponent<D>(out _);
        e.TryGetComponent<E>(out _);
        e.TryGetComponent<F>(out _);
    }
}
""";
        var diags = AnalyzerTestHarness.Run(
            new BridgePullPatternAnalyzer(),
            "Ark.Gameplay.Combat",
            src, Components);
        Assert.Empty(diags);
    }
}
