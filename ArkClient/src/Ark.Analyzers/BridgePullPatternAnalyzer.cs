using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ark.Analyzers;

/// <summary>
/// ECS007: <c>Ark.Bridge.*</c> 方法内的 ECS 组件拉取调用过多 — 建议改造为 Sync System 推送范式。
///
/// <para>计数对象：<c>Friflo.Engine.ECS.Entity.TryGetComponent&lt;T&gt;</c>
/// 与 <c>Friflo.Engine.ECS.Entity.GetComponent&lt;T&gt;</c>。同一方法内出现 ≥
/// <see cref="PullThreshold"/> 次即报告（默认 5）。</para>
///
/// <para>豁免：</para>
/// <list type="bullet">
///   <item>容器类型标 <c>[EcsAuthorityBridge]</c> 或 <c>[EcsAuthoritySystemNode]</c></item>
///   <item>类型位于 <c>Ark.Systems.Sync</c> 命名空间（Sync System 自身就是收集 + 推送）</item>
///   <item>类型位于 <c>Ark.Ecs.Bridge</c> 命名空间</item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BridgePullPatternAnalyzer : DiagnosticAnalyzer
{
    public const int PullThreshold = 5;

    private const string BridgeAssemblyPrefix = "Ark.Bridge";
    private const string AuthorityNodeAttributeFullName = "Ark.Analyzers.Attributes.EcsAuthoritySystemNodeAttribute";
    private const string AuthorityBridgeAttributeFullName = "Ark.Analyzers.Attributes.EcsAuthorityBridgeAttribute";
    private const string SyncNamespace = "Ark.Systems.Sync";
    private const string BridgeComponentNamespace = "Ark.Ecs.Bridge";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.BridgeExcessivePullPattern);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var asmName = start.Compilation.AssemblyName ?? string.Empty;
            if (!IsBridgeAssembly(asmName))
                return;

            start.RegisterOperationBlockAction(AnalyzeMethodBody);
        });
    }

    private static bool IsBridgeAssembly(string assemblyName)
    {
        if (!assemblyName.StartsWith(BridgeAssemblyPrefix, StringComparison.Ordinal))
            return false;
        return assemblyName.Length == BridgeAssemblyPrefix.Length
            || assemblyName[BridgeAssemblyPrefix.Length] == '.';
    }

    private static void AnalyzeMethodBody(OperationBlockAnalysisContext context)
    {
        if (context.OwningSymbol is not IMethodSymbol method)
            return;

        var containing = method.ContainingType;
        if (containing is null)
            return;

        if (IsExempt(containing))
            return;

        int pullCount = 0;
        foreach (var block in context.OperationBlocks)
        {
            foreach (var op in block.Descendants())
            {
                if (op is IInvocationOperation invocation && IsEntityPull(invocation.TargetMethod))
                    pullCount++;
            }
        }

        if (pullCount < PullThreshold)
            return;

        var location = method.Locations.FirstOrDefault() ?? Location.None;
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.BridgeExcessivePullPattern,
            location,
            $"{containing.Name}.{method.Name}",
            pullCount));
    }

    private static bool IsEntityPull(IMethodSymbol method)
    {
        if (method.Name != "TryGetComponent" && method.Name != "GetComponent")
            return false;

        var containingTypeFqn = method.ContainingType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return containingTypeFqn == "global::Friflo.Engine.ECS.Entity";
    }

    private static bool IsExempt(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (ns == SyncNamespace || ns.StartsWith(SyncNamespace + ".", StringComparison.Ordinal))
            return true;
        if (ns == BridgeComponentNamespace || ns.StartsWith(BridgeComponentNamespace + ".", StringComparison.Ordinal))
            return true;

        for (var t = (INamedTypeSymbol?)type; t is not null; t = t.BaseType)
        {
            if (HasAttribute(t, AuthorityNodeAttributeFullName))
                return true;
            if (HasAttribute(t, AuthorityBridgeAttributeFullName))
                return true;
        }
        return false;
    }

    private static bool HasAttribute(ISymbol symbol, string attributeFullName)
    {
        return symbol.GetAttributes().Any(a =>
            string.Equals(a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                "global::" + attributeFullName, StringComparison.Ordinal));
    }
}
