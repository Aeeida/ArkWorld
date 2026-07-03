using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ark.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EcsFirstSingleSourceAnalyzer : DiagnosticAnalyzer
{
    private const string ControlAuthorityFieldAttributeFullName = "Ark.Analyzers.Attributes.ControlAuthorityFieldAttribute";
    private const string ControlAuthoritySourceAttributeFullName = "Ark.Analyzers.Attributes.ControlAuthoritySourceAttribute";
    private const string ControlAuthorityResolverAttributeFullName = "Ark.Analyzers.Attributes.ControlAuthorityResolverAttribute";

    private static readonly ImmutableHashSet<string> StructuralChangeMethods =
        ImmutableHashSet.Create(StringComparer.Ordinal,
            "AddComponent", "RemoveComponent", "DeleteEntity", "CreateEntity",
            "AddTag", "RemoveTag", "AddTags", "RemoveTags");

    /// <summary>Friflo Query lambda 枚举方法名，进入这些调用的 lambda 体也视为 query 循环。</summary>
    private static readonly ImmutableHashSet<string> QueryEnumerationMethods =
        ImmutableHashSet.Create(StringComparer.Ordinal,
            "ForEachEntity", "For", "ForEach", "Each");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            DiagnosticDescriptors.EcsSingleSourceViolation,
            DiagnosticDescriptors.EcsStructuralChangeInsideQueryLoop,
            DiagnosticDescriptors.EcsTelemetryUsedAsControlAuthority);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterOperationAction(AnalyzeAssignment, OperationKind.SimpleAssignment);
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeAssignment(OperationAnalysisContext context)
    {
        if (context.Operation is not ISimpleAssignmentOperation assignment)
            return;

        if (assignment.Target is not IFieldReferenceOperation fieldRef)
            return;

        // Discover [ControlAuthorityField] on the target field
        var authorityAttr = fieldRef.Field.GetAttributes().FirstOrDefault(a =>
            string.Equals(a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                "global::" + ControlAuthorityFieldAttributeFullName, StringComparison.Ordinal));
        if (authorityAttr is null)
            return;

        var authoritySource = authorityAttr.ConstructorArguments.Length > 0
            ? authorityAttr.ConstructorArguments[0].Value as string ?? string.Empty
            : string.Empty;

        if (!string.IsNullOrEmpty(authoritySource) && IsInsideAuthoritySourceLoop(assignment.Syntax, authoritySource))
            return;

        if (RhsReadsAuthoritySourceComponent(assignment.Value))
            return;

        if (context.ContainingSymbol is not IMethodSymbol methodSymbol)
            return;

        var fullFieldName = $"{fieldRef.Field.ContainingType?.Name}.{fieldRef.Field.Name}";
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.EcsSingleSourceViolation,
            assignment.Syntax.GetLocation(),
            methodSymbol.Name,
            fullFieldName,
            string.IsNullOrEmpty(authoritySource) ? "non-authority context" : authoritySource));
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        if (context.Operation is not IInvocationOperation invocation)
            return;

        var targetMethod = invocation.TargetMethod;
        if (IsTelemetryAuthorityRead(invocation, context))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.EcsTelemetryUsedAsControlAuthority,
                invocation.Syntax.GetLocation(),
                context.ContainingSymbol.Name,
                invocation.TargetMethod.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            return;
        }

        if (!StructuralChangeMethods.Contains(targetMethod.Name))
            return;

        if (!IsEcsStructuralMutation(targetMethod))
            return;

        if (!IsInsideQueryChunkLoop(invocation))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.EcsStructuralChangeInsideQueryLoop,
            invocation.Syntax.GetLocation(),
            targetMethod.Name));
    }

    private static bool IsEcsStructuralMutation(IMethodSymbol method)
    {
        var containingType = method.ContainingType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return containingType is "global::Friflo.Engine.ECS.Entity"
            or "global::Friflo.Engine.ECS.EntityStore"
            or "global::Friflo.Engine.ECS.CommandBuffer";
    }

    private static bool IsInsideQueryChunkLoop(IOperation operation)
    {
        for (SyntaxNode? node = operation.Syntax; node is not null; node = node.Parent)
        {
            // Form A: foreach (var chunk in query.Chunks) { ... }
            if (node is ForEachStatementSyntax forEach
                && forEach.Expression is MemberAccessExpressionSyntax memberAccess
                && string.Equals(memberAccess.Name.Identifier.ValueText, "Chunks", StringComparison.Ordinal))
            {
                return true;
            }

            // Form B: query.ForEachEntity((ref T c, Entity e) => { ... }) / query.For(...) / query.Each(...)
            // 判定条件：当前节点是 lambda/anonymous-method body，且父调用是 Friflo Query 枚举方法。
            if (node is LambdaExpressionSyntax || node is AnonymousMethodExpressionSyntax)
            {
                if (TryFindEnclosingQueryEnumeration(node))
                    return true;
            }
        }

        return false;
    }

    private static bool TryFindEnclosingQueryEnumeration(SyntaxNode lambdaOrAnonymous)
    {
        // Walk up: lambda -> Argument -> ArgumentList -> InvocationExpression
        for (SyntaxNode? p = lambdaOrAnonymous.Parent; p is not null; p = p.Parent)
        {
            if (p is InvocationExpressionSyntax invocation)
            {
                if (invocation.Expression is MemberAccessExpressionSyntax ma
                    && QueryEnumerationMethods.Contains(ma.Name.Identifier.ValueText))
                {
                    return true;
                }
                return false;
            }
            if (p is StatementSyntax || p is MemberDeclarationSyntax)
                return false;
        }
        return false;
    }

    private static bool IsInsideAuthoritySourceLoop(SyntaxNode syntax, string queueName)
    {
        for (SyntaxNode? node = syntax; node is not null; node = node.Parent)
        {
            if (node is not WhileStatementSyntax whileStatement)
                continue;

            if (whileStatement.Condition is InvocationExpressionSyntax invocation
                && invocation.Expression is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Expression is IdentifierNameSyntax id
                && string.Equals(id.Identifier.ValueText, queueName, StringComparison.Ordinal)
                && string.Equals(memberAccess.Name.Identifier.ValueText, "TryDequeue", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool RhsReadsAuthoritySourceComponent(IOperation? rhs)
    {
        if (rhs is null)
            return false;

        foreach (var descendant in DescendantsAndSelf(rhs))
        {
            ITypeSymbol? rootType = descendant switch
            {
                IFieldReferenceOperation fr => fr.Instance?.Type,
                IPropertyReferenceOperation pr => pr.Instance?.Type,
                _ => null
            };
            if (rootType is INamedTypeSymbol named
                && named.GetAttributes().Any(a =>
                    string.Equals(a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        "global::" + ControlAuthoritySourceAttributeFullName, StringComparison.Ordinal)))
            {
                return true;
            }
        }
        return false;
    }

    private static System.Collections.Generic.IEnumerable<IOperation> DescendantsAndSelf(IOperation op)
    {
        yield return op;
        foreach (var child in op.ChildOperations)
            foreach (var d in DescendantsAndSelf(child))
                yield return d;
    }

    private static bool IsTelemetryAuthorityRead(IInvocationOperation invocation, OperationAnalysisContext context)
    {
        // Phase 1.2: 仅在包含方法标了 [ControlAuthorityResolver] 时启用检查。
        if (context.ContainingSymbol is not IMethodSymbol containingMethod)
            return false;

        var hasResolverAttr = containingMethod.GetAttributes().Any(a =>
            string.Equals(a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                "global::" + ControlAuthorityResolverAttributeFullName, StringComparison.Ordinal));
        if (!hasResolverAttr)
            return false;

        if (!string.Equals(invocation.TargetMethod.Name, "TryGetComponent", StringComparison.Ordinal)
            || invocation.TargetMethod.TypeArguments.Length != 1)
        {
            return false;
        }

        // 读取任意标了 [ControlAuthoritySource] 的组件也豁免（它才是 authority source）。
        // 这里反向检查：只要不是 authority source 的 component，都不能被 resolver 引用。
        var componentTypeArg = invocation.TargetMethod.TypeArguments[0];
        var isAuthoritySource = componentTypeArg is INamedTypeSymbol named
            && named.GetAttributes().Any(a =>
                string.Equals(a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    "global::" + ControlAuthoritySourceAttributeFullName, StringComparison.Ordinal));
        return !isAuthoritySource;
    }
}
