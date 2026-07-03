using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ark.Analyzers;

/// <summary>
/// ARK003: Godot Node 派生类禁止直接 <c>new</c> 一个标记 <c>[ServiceClass]</c> 的服务/缓存类型。
/// 服务实例必须由 Bootstrap/DI 创建，Node 应通过 GameServices 解析。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ServiceNewedFromGodotNodeAnalyzer : DiagnosticAnalyzer
{
    private const string ServiceClassAttributeFullName = "Ark.Analyzers.Attributes.ServiceClassAttribute";
    private const string GodotNodeFullName = "Godot.Node";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.ServiceNewedFromGodotNode);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startCtx =>
        {
            var serviceAttr = startCtx.Compilation.GetTypeByMetadataName(ServiceClassAttributeFullName);
            var godotNode = startCtx.Compilation.GetTypeByMetadataName(GodotNodeFullName);
            if (serviceAttr is null || godotNode is null)
                return;

            startCtx.RegisterOperationAction(
                opCtx => AnalyzeObjectCreation(opCtx, serviceAttr, godotNode),
                OperationKind.ObjectCreation);
        });
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context, INamedTypeSymbol serviceAttr, INamedTypeSymbol godotNode)
    {
        if (context.Operation is not IObjectCreationOperation creation)
            return;

        if (creation.Type is not INamedTypeSymbol createdType)
            return;

        if (!HasAttribute(createdType, serviceAttr))
            return;

        if (context.ContainingSymbol.ContainingType is not INamedTypeSymbol containing)
            return;

        if (!InheritsFrom(containing, godotNode))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ServiceNewedFromGodotNode,
            creation.Syntax.GetLocation(),
            containing.Name,
            createdType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    private static bool HasAttribute(INamedTypeSymbol type, INamedTypeSymbol attrType)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            foreach (var a in t.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrType))
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
