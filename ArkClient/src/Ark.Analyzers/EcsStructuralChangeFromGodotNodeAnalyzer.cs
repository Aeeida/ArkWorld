using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ark.Analyzers;

/// <summary>
/// ARK005: 禁止 Godot Node 派生类对 Friflo ECS 进行结构变更
/// (<c>AddComponent / RemoveComponent / AddTag / RemoveTag / DeleteEntity / CreateEntity</c> 等)。
/// 表现层 Node 应只读 ECS 或写"意图组件"，让 ECS 系统执行权威写入。
/// 仅 <c>[EcsAuthoritySystemNode]</c> 标注的 Bootstrap 节点豁免。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EcsStructuralChangeFromGodotNodeAnalyzer : DiagnosticAnalyzer
{
    private const string GodotNodeFullName = "Godot.Node";
    private const string AuthorityNodeAttributeFullName = "Ark.Analyzers.Attributes.EcsAuthoritySystemNodeAttribute";

    private static readonly ImmutableHashSet<string> StructuralChangeMethods =
        ImmutableHashSet.Create(StringComparer.Ordinal,
            "AddComponent", "RemoveComponent", "DeleteEntity", "CreateEntity",
            "AddTag", "RemoveTag", "AddTags", "RemoveTags");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.EcsStructuralChangeFromGodotNode);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var godotNodeSymbol = start.Compilation.GetTypeByMetadataName(GodotNodeFullName);
            if (godotNodeSymbol is null)
                return;

            start.RegisterOperationAction(
                ctx => Analyze(ctx, godotNodeSymbol),
                OperationKind.Invocation);
        });
    }

    private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol godotNodeSymbol)
    {
        if (context.Operation is not IInvocationOperation invocation)
            return;

        var method = invocation.TargetMethod;
        if (!StructuralChangeMethods.Contains(method.Name))
            return;

        if (!IsEcsStructuralMutation(method))
            return;

        var containingType = context.ContainingSymbol?.ContainingType;
        if (containingType is null)
            return;

        if (!InheritsFrom(containingType, godotNodeSymbol))
            return;

        // 豁免：标 [EcsAuthoritySystemNode] 的 Node。
        if (HasAuthorityAttribute(containingType))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.EcsStructuralChangeFromGodotNode,
            invocation.Syntax.GetLocation(),
            containingType.Name,
            method.Name));
    }

    private static bool IsEcsStructuralMutation(IMethodSymbol method)
    {
        var containingType = method.ContainingType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return containingType is "global::Friflo.Engine.ECS.Entity"
            or "global::Friflo.Engine.ECS.EntityStore"
            or "global::Friflo.Engine.ECS.CommandBuffer";
    }

    private static bool HasAuthorityAttribute(INamedTypeSymbol type)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            if (t.GetAttributes().Any(a =>
                string.Equals(a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    "global::" + AuthorityNodeAttributeFullName, StringComparison.Ordinal)))
            {
                return true;
            }
        }
        return false;
    }

    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(t, baseType))
                return true;
        }
        return false;
    }
}
