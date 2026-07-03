using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ark.Analyzers.Tests;

/// <summary>
/// Lightweight Roslyn harness: compiles a fake assembly under a chosen name,
/// runs the analyzer, returns the produced diagnostics filtered to the analyzer's IDs.
/// </summary>
internal static class AnalyzerTestHarness
{
    private static readonly ImmutableArray<MetadataReference> CoreReferences = LoadCoreReferences();

    public static ImmutableArray<Diagnostic> Run(
        DiagnosticAnalyzer analyzer,
        string assemblyName,
        string source,
        params string[] additionalSources)
    {
        var trees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)),
            CSharpSyntaxTree.ParseText(StubSource, new CSharpParseOptions(LanguageVersion.Latest)),
        };
        foreach (var s in additionalSources)
            trees.Add(CSharpSyntaxTree.ParseText(s, new CSharpParseOptions(LanguageVersion.Latest)));

        var compilation = CSharpCompilation.Create(
            assemblyName,
            trees,
            CoreReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create(analyzer));
        var diagnostics = withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
        var ids = analyzer.SupportedDiagnostics.Select(d => d.Id).ToHashSet();
        return diagnostics.Where(d => ids.Contains(d.Id)).ToImmutableArray();
    }

    private static ImmutableArray<MetadataReference> LoadCoreReferences()
    {
        // Pull TPA from the runtime so System.Runtime / netstandard / etc. resolve.
        var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        var refs = new List<MetadataReference>();
        foreach (var path in trusted.Split(Path.PathSeparator))
        {
            if (string.IsNullOrEmpty(path)) continue;
            try { refs.Add(MetadataReference.CreateFromFile(path)); }
            catch { /* skip native or unreadable */ }
        }

        // Add Analyzer attributes assembly so [BridgeWritableComponent], etc. are available.
        var attrAsm = typeof(Ark.Analyzers.Attributes.BridgeWritableComponentAttribute).Assembly.Location;
        if (!string.IsNullOrEmpty(attrAsm))
            refs.Add(MetadataReference.CreateFromFile(attrAsm));

        return refs.ToImmutableArray();
    }

    /// <summary>
    /// Minimal stand-in for Friflo.Engine.ECS.Entity / EntityStore / CommandBuffer
    /// matching the fully-qualified names the analyzers probe for.
    /// </summary>
    private const string StubSource = """
namespace Friflo.Engine.ECS;

public struct Entity
{
    public bool TryGetComponent<T>(out T value) where T : struct { value = default; return false; }
    public ref T GetComponent<T>() where T : struct => throw new System.NotImplementedException();
    public void AddComponent<T>() where T : struct { }
    public void AddComponent<T>(in T value) where T : struct { }
    public void RemoveComponent<T>() where T : struct { }
    public void AddTag<T>() where T : struct { }
    public void RemoveTag<T>() where T : struct { }
}

public sealed class EntityStore { }
public sealed class CommandBuffer { }
""";
}
