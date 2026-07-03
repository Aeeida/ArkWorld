using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ark.Analyzers;

/// <summary>
/// ECS004: 在 <c>Ark.Services.Remote.*</c> 命名空间内手写 DTO→Component 字段拷贝时报警。
/// 仅当 LHS 字段所属组件类型恰好是 RHS DTO 已声明映射的目标时触发，
/// 提示改用 Source Generator 生成的 <c>ApplyToEcs/ToXxx</c> 扩展方法。
/// 字段标记 <c>[EcsComputedField]</c> 可豁免（属于派生/计算字段）。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EcsManualMappingAnalyzer : DiagnosticAnalyzer
{
    private const string MapToEcsComponentAttributeFullName = "Ark.Analyzers.Attributes.MapToEcsComponentAttribute";
    private const string ExternalDtoMappingAttributeFullName = "Ark.Analyzers.Attributes.ExternalDtoMappingAttribute";
    private const string EcsComputedFieldAttributeFullName = "Ark.Analyzers.Attributes.EcsComputedFieldAttribute";
    private const string ProjectionNamespacePrefix = "Ark.Services.Remote";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.EcsManualDtoMapping);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startCtx =>
        {
            var mapAttr = startCtx.Compilation.GetTypeByMetadataName(MapToEcsComponentAttributeFullName);
            var externalAttr = startCtx.Compilation.GetTypeByMetadataName(ExternalDtoMappingAttributeFullName);
            if (mapAttr is null && externalAttr is null)
                return;

            var dtoToComponents = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            if (externalAttr is not null)
            {
                foreach (var attrData in startCtx.Compilation.Assembly.GetAttributes())
                {
                    if (!IsAttribute(attrData.AttributeClass, ExternalDtoMappingAttributeFullName))
                        continue;
                    if (attrData.ConstructorArguments.Length < 2)
                        continue;
                    if (attrData.ConstructorArguments[0].Value is not INamedTypeSymbol dtoType
                        || attrData.ConstructorArguments[1].Value is not INamedTypeSymbol compType)
                        continue;
                    AddMapping(dtoToComponents, dtoType, compType);
                }
            }

            startCtx.RegisterOperationAction(opCtx =>
                AnalyzeAssignment(opCtx, dtoToComponents),
                OperationKind.SimpleAssignment);
        });
    }

    private static void AnalyzeAssignment(
        OperationAnalysisContext context,
        Dictionary<string, HashSet<string>> externalDtoToComponents)
    {
        if (context.ContainingSymbol is not IMethodSymbol method)
            return;

        var ns = method.ContainingType?.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (!ns.StartsWith(ProjectionNamespacePrefix, StringComparison.Ordinal))
            return;

        if (context.Operation is not ISimpleAssignmentOperation assignment)
            return;

        if (assignment.Target is not IFieldReferenceOperation fieldRef)
            return;

        var lhsCompType = fieldRef.Field.ContainingType;
        if (lhsCompType is null)
            return;

        var lhsCompTypeNs = lhsCompType.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (!lhsCompTypeNs.StartsWith("Ark.Ecs.Components", StringComparison.Ordinal))
            return;

        if (fieldRef.Field.GetAttributes().Any(a => IsAttribute(a.AttributeClass, EcsComputedFieldAttributeFullName)))
            return;

        var lhsCompKey = lhsCompType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (!RhsReferencesMatchingDto(assignment.Value, lhsCompKey, externalDtoToComponents, out var dtoTypeName))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.EcsManualDtoMapping,
            assignment.Syntax.GetLocation(),
            $"{lhsCompType.Name}.{fieldRef.Field.Name}",
            dtoTypeName));
    }

    private static bool RhsReferencesMatchingDto(
        IOperation? rhs,
        string lhsCompKey,
        Dictionary<string, HashSet<string>> externalDtoToComponents,
        out string dtoTypeName)
    {
        dtoTypeName = string.Empty;
        if (rhs is null)
            return false;

        foreach (var descendant in rhs.DescendantsAndSelf())
        {
            ITypeSymbol? rootType = descendant switch
            {
                IPropertyReferenceOperation pr => pr.Instance?.Type,
                IFieldReferenceOperation fr => fr.Instance?.Type,
                _ => null
            };
            if (rootType is not INamedTypeSymbol named)
                continue;

            // Source-declared [MapToEcsComponent] on the DTO
            foreach (var attr in named.GetAttributes())
            {
                if (!IsAttribute(attr.AttributeClass, MapToEcsComponentAttributeFullName))
                    continue;
                if (attr.ConstructorArguments.Length == 0)
                    continue;
                if (attr.ConstructorArguments[0].Value is not INamedTypeSymbol compType)
                    continue;
                if (string.Equals(
                        compType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        lhsCompKey, StringComparison.Ordinal))
                {
                    dtoTypeName = named.Name;
                    return true;
                }
            }

            // Assembly-level [ExternalDtoMapping]
            var dtoKey = named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (externalDtoToComponents.TryGetValue(dtoKey, out var compSet)
                && compSet.Contains(lhsCompKey))
            {
                dtoTypeName = named.Name;
                return true;
            }
        }

        return false;
    }

    private static void AddMapping(Dictionary<string, HashSet<string>> map, INamedTypeSymbol dto, INamedTypeSymbol comp)
    {
        var dtoKey = dto.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var compKey = comp.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (!map.TryGetValue(dtoKey, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            map[dtoKey] = set;
        }
        set.Add(compKey);
    }

    private static bool IsAttribute(INamedTypeSymbol? attrClass, string fullName)
    {
        if (attrClass is null)
            return false;
        var display = attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return string.Equals(display, "global::" + fullName, StringComparison.Ordinal);
    }
}

internal static class OperationExtensions
{
    public static IEnumerable<IOperation> DescendantsAndSelf(this IOperation op)
    {
        yield return op;
        foreach (var child in op.ChildOperations)
        {
            foreach (var d in child.DescendantsAndSelf())
                yield return d;
        }
    }
}
