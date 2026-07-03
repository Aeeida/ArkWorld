using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ark.Analyzers;

/// <summary>
/// ECS005: 禁止 <c>Ark.Bridge.*</c> 程序集对未标 <c>[BridgeWritableComponent]</c>
/// / <c>[BridgeWritableTag]</c> 的业务组件/Tag 进行写入。
///
/// <para>检测的写入形式：</para>
/// <list type="bullet">
///   <item><c>entity.AddComponent&lt;T&gt;(...)</c> / <c>entity.AddComponent(new T())</c></item>
///   <item><c>entity.RemoveComponent&lt;T&gt;()</c></item>
///   <item><c>entity.AddTag&lt;T&gt;()</c> / <c>entity.RemoveTag&lt;T&gt;()</c></item>
///   <item><c>entity.GetComponent&lt;T&gt;() = ...</c>（左值赋值，直接重写组件值）</item>
/// </list>
///
/// <para>豁免：</para>
/// <list type="bullet">
///   <item>组件类型标了 <c>[BridgeWritableComponent]</c>（例如表现反馈类）</item>
///   <item>组件位于 <c>Ark.Ecs.Bridge</c> 命名空间（NodeRef / RidRef 等桥接组件）</item>
///   <item>容器类型标了 <c>[EcsAuthoritySystemNode]</c>（Bootstrap 系统宿主）</item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BridgeBusinessComponentWriteAnalyzer : DiagnosticAnalyzer
{
    private const string BridgeAssemblyPrefix = "Ark.Bridge";
    private const string BridgeWritableComponentAttributeFullName = "Ark.Analyzers.Attributes.BridgeWritableComponentAttribute";
    private const string BridgeWritableTagAttributeFullName = "Ark.Analyzers.Attributes.BridgeWritableTagAttribute";
    private const string AuthorityNodeAttributeFullName = "Ark.Analyzers.Attributes.EcsAuthoritySystemNodeAttribute";
    private const string AuthorityBridgeAttributeFullName = "Ark.Analyzers.Attributes.EcsAuthorityBridgeAttribute";
    private const string BridgeComponentNamespace = "Ark.Ecs.Bridge";

    private static readonly ImmutableHashSet<string> ComponentWriteMethods =
        ImmutableHashSet.Create(StringComparer.Ordinal,
            "AddComponent", "RemoveComponent", "AddComponents", "RemoveComponents");

    private static readonly ImmutableHashSet<string> TagWriteMethods =
        ImmutableHashSet.Create(StringComparer.Ordinal,
            "AddTag", "RemoveTag", "AddTags", "RemoveTags");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.BridgeWritesBusinessComponent);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var asmName = start.Compilation.AssemblyName ?? string.Empty;
            if (!IsBridgeAssembly(asmName))
                return;

            start.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
            start.RegisterOperationAction(AnalyzeAssignment, OperationKind.SimpleAssignment);
        });
    }

    private static bool IsBridgeAssembly(string assemblyName)
    {
        if (!assemblyName.StartsWith(BridgeAssemblyPrefix, StringComparison.Ordinal))
            return false;
        // Exact match (Ark.Bridge) or sub-namespace (Ark.Bridge.Player / Ark.Bridge.Building).
        return assemblyName.Length == BridgeAssemblyPrefix.Length
            || assemblyName[BridgeAssemblyPrefix.Length] == '.';
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        if (context.Operation is not IInvocationOperation invocation)
            return;

        var method = invocation.TargetMethod;
        var isComponentWrite = ComponentWriteMethods.Contains(method.Name);
        var isTagWrite = !isComponentWrite && TagWriteMethods.Contains(method.Name);
        if (!isComponentWrite && !isTagWrite)
            return;

        if (!IsEcsStructuralMutation(method))
            return;

        if (IsContainingTypeAuthorityNode(context.ContainingSymbol))
            return;

        var targetType = ExtractTargetComponentType(invocation);
        if (targetType is null)
            return;

        if (IsBridgeAllowed(targetType, isTagWrite))
            return;

        Report(context, invocation.Syntax.GetLocation(), targetType, method.Name);
    }

    private static void AnalyzeAssignment(OperationAnalysisContext context)
    {
        if (context.Operation is not ISimpleAssignmentOperation assignment)
            return;

        // Pattern: entity.GetComponent<T>() = ...;
        if (assignment.Target is not IInvocationOperation invocation)
            return;

        var method = invocation.TargetMethod;
        if (!string.Equals(method.Name, "GetComponent", StringComparison.Ordinal))
            return;

        if (!IsEcsStructuralMutation(method) && !IsEcsRefAccessor(method))
            return;

        if (IsContainingTypeAuthorityNode(context.ContainingSymbol))
            return;

        var targetType = ExtractTargetComponentType(invocation);
        if (targetType is null)
            return;

        if (IsBridgeAllowed(targetType, isTag: false))
            return;

        Report(context, assignment.Syntax.GetLocation(), targetType, "GetComponent<T>() = …");
    }

    private static void Report(OperationAnalysisContext context, Location location, ITypeSymbol component, string operation)
    {
        var containingType = context.ContainingSymbol?.ContainingType?.Name ?? "<unknown>";
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.BridgeWritesBusinessComponent,
            location,
            containingType,
            component.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            operation));
    }

    private static bool IsEcsStructuralMutation(IMethodSymbol method)
    {
        var containingType = method.ContainingType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return containingType is "global::Friflo.Engine.ECS.Entity"
            or "global::Friflo.Engine.ECS.EntityStore"
            or "global::Friflo.Engine.ECS.CommandBuffer";
    }

    private static bool IsEcsRefAccessor(IMethodSymbol method)
    {
        // Friflo's GetComponent<T>() returns ref T; ContainingType still Entity.
        var containingType = method.ContainingType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return containingType == "global::Friflo.Engine.ECS.Entity";
    }

    private static ITypeSymbol? ExtractTargetComponentType(IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;
        if (method.TypeArguments.Length > 0)
            return method.TypeArguments[0];

        // Non-generic AddComponent(new T()) form: infer from first argument.
        if (invocation.Arguments.Length > 0)
            return invocation.Arguments[0].Value.Type;

        return null;
    }

    private static bool IsBridgeAllowed(ITypeSymbol component, bool isTag)
    {
        // Bridge-component allow-list: Ark.Ecs.Bridge.* (NodeRef / RidRef / etc.)
        if (IsInNamespace(component, BridgeComponentNamespace))
            return true;

        var allowAttr = isTag ? BridgeWritableTagAttributeFullName : BridgeWritableComponentAttributeFullName;
        // Components may be tagged with either; tags occasionally tagged with the component variant.
        return HasAttribute(component, allowAttr)
            || HasAttribute(component, BridgeWritableComponentAttributeFullName)
            || HasAttribute(component, BridgeWritableTagAttributeFullName);
    }

    private static bool IsInNamespace(ITypeSymbol type, string fullNamespace)
    {
        var ns = type.ContainingNamespace;
        if (ns is null) return false;
        var display = ns.ToDisplayString();
        return string.Equals(display, fullNamespace, StringComparison.Ordinal)
            || display.StartsWith(fullNamespace + ".", StringComparison.Ordinal);
    }

    private static bool HasAttribute(ISymbol symbol, string attributeFullName)
    {
        return symbol.GetAttributes().Any(a =>
            string.Equals(a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                "global::" + attributeFullName, StringComparison.Ordinal));
    }

    private static bool IsContainingTypeAuthorityNode(ISymbol? containing)
    {
        var type = containing?.ContainingType;
        for (var t = type; t is not null; t = t.BaseType)
        {
            if (HasAttribute(t, AuthorityNodeAttributeFullName))
                return true;
            if (HasAttribute(t, AuthorityBridgeAttributeFullName))
                return true;
        }
        return false;
    }
}
