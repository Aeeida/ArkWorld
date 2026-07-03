using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ark.Analyzers;

/// <summary>
/// ARK004: 程序集级 <c>[ForbidGodotDependency]</c> 强制本程序集与 Godot 完全解耦。
/// 任何对 <c>Godot.*</c> 命名空间下类型的引用（字段/属性/方法签名/类型表达式/调用）都会被报告。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NetworkLayerGodotDependencyAnalyzer : DiagnosticAnalyzer
{
    private const string ForbidGodotDependencyAttributeFullName =
        "Ark.Analyzers.Attributes.ForbidGodotDependencyAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.NetworkLayerGodotDependency);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var attrSymbol = compilationContext.Compilation.GetTypeByMetadataName(ForbidGodotDependencyAttributeFullName);
            if (attrSymbol is null)
                return;

            bool forbidden = false;
            foreach (var attr in compilationContext.Compilation.Assembly.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrSymbol))
                {
                    forbidden = true;
                    break;
                }
            }
            if (!forbidden)
                return;

            var assemblyName = compilationContext.Compilation.AssemblyName ?? "<unknown>";

            compilationContext.RegisterOperationAction(
                ctx => CheckOperation(ctx, assemblyName),
                OperationKind.Invocation,
                OperationKind.ObjectCreation,
                OperationKind.FieldReference,
                OperationKind.PropertyReference,
                OperationKind.MethodReference,
                OperationKind.TypeOf);

            compilationContext.RegisterSymbolAction(
                ctx => CheckSymbol(ctx, assemblyName),
                SymbolKind.Field,
                SymbolKind.Property,
                SymbolKind.Method,
                SymbolKind.Parameter);
        });
    }

    private static void CheckOperation(OperationAnalysisContext context, string assemblyName)
    {
        ITypeSymbol? type = context.Operation switch
        {
            IInvocationOperation inv => inv.TargetMethod.ContainingType,
            IObjectCreationOperation obj => obj.Type,
            IFieldReferenceOperation fr => fr.Field.ContainingType,
            IPropertyReferenceOperation pr => pr.Property.ContainingType,
            IMethodReferenceOperation mr => mr.Method.ContainingType,
            ITypeOfOperation to => to.TypeOperand,
            _ => null,
        };
        if (IsGodotType(type))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NetworkLayerGodotDependency,
                context.Operation.Syntax.GetLocation(),
                type!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                assemblyName));
        }
    }

    private static void CheckSymbol(SymbolAnalysisContext context, string assemblyName)
    {
        ITypeSymbol? type = context.Symbol switch
        {
            IFieldSymbol f => f.Type,
            IPropertySymbol p => p.Type,
            IMethodSymbol m => m.ReturnType,
            IParameterSymbol p => p.Type,
            _ => null,
        };
        if (!IsGodotType(type))
            return;

        var location = context.Symbol.Locations.Length > 0 ? context.Symbol.Locations[0] : Location.None;
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.NetworkLayerGodotDependency,
            location,
            type!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            assemblyName));
    }

    private static bool IsGodotType(ITypeSymbol? type)
    {
        if (type is null)
            return false;

        // Strip array/pointer/by-ref wrappers
        while (true)
        {
            switch (type)
            {
                case IArrayTypeSymbol at: type = at.ElementType; continue;
                case IPointerTypeSymbol pt: type = pt.PointedAtType; continue;
            }
            break;
        }

        var ns = type.ContainingNamespace;
        while (ns is not null && !ns.IsGlobalNamespace)
        {
            if (string.Equals(ns.Name, "Godot", StringComparison.Ordinal)
                && (ns.ContainingNamespace is null || ns.ContainingNamespace.IsGlobalNamespace))
            {
                return true;
            }
            ns = ns.ContainingNamespace;
        }
        return false;
    }
}
