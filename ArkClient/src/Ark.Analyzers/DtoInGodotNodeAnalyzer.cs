using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ark.Analyzers;

/// <summary>
/// Roslyn Analyzer：禁止在 Godot Node 派生类中直接使用网络 DTO 类型。
/// <list type="bullet">
///   <item><b>ARK001</b>: DTO 类型作为字段/属性出现在 Node 派生类中。</item>
///   <item><b>ARK002</b>: DTO 类型作为方法参数/返回值/局部变量出现在 Node 派生类中。</item>
/// </list>
/// 
/// 判定逻辑：
/// <list type="number">
///   <item>当前类型继承自 <c>Godot.Node</c>（直接或间接）。</item>
///   <item>引用的类型属于受禁 DTO 命名空间（硬编码 + 程序集级 <c>[ForbidDtoInGodot]</c> 配置）。</item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DtoInGodotNodeAnalyzer : DiagnosticAnalyzer
{
    // 默认禁止的 DTO 命名空间前缀
    private static readonly string[] DefaultForbiddenPrefixes =
    {
        "Game.Shared.Core.DTOs",
        "Game.Shared.Protocols.Messages"
    };

    private const string ForbidAttributeFullName = "Ark.Analyzers.Attributes.ForbidDtoInGodotAttribute";
    private const string GodotNodeFullName = "Godot.Node";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            DiagnosticDescriptors.DtoInGodotNode,
            DiagnosticDescriptors.DtoInGodotMethod);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var godotNodeSymbol = compilationContext.Compilation.GetTypeByMetadataName(GodotNodeFullName);
            if (godotNodeSymbol is null)
                return; // Not a Godot project — skip

            // Collect forbidden DTO namespace prefixes from assembly-level attributes
            var forbiddenPrefixes = CollectForbiddenPrefixes(compilationContext.Compilation);

            // Analyze named type declarations (classes)
            compilationContext.RegisterSymbolAction(
                ctx => AnalyzeNamedType(ctx, godotNodeSymbol, forbiddenPrefixes),
                SymbolKind.NamedType);

            // Analyze method declarations for parameter/return/local DTO usage
            compilationContext.RegisterSyntaxNodeAction(
                ctx => AnalyzeMethodDeclaration(ctx, godotNodeSymbol, forbiddenPrefixes),
                SyntaxKind.MethodDeclaration);
        });
    }

    /// <summary>
    /// 收集程序集级 <c>[ForbidDtoInGodot]</c> 中声明的额外命名空间前缀。
    /// </summary>
    private static ImmutableArray<string> CollectForbiddenPrefixes(Compilation compilation)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        builder.AddRange(DefaultForbiddenPrefixes);

        var forbidAttrSymbol = compilation.GetTypeByMetadataName(ForbidAttributeFullName);
        if (forbidAttrSymbol is null)
            return builder.ToImmutable();

        foreach (var attrData in compilation.Assembly.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attrData.AttributeClass, forbidAttrSymbol))
                continue;
            if (attrData.ConstructorArguments.Length == 1 &&
                attrData.ConstructorArguments[0].Value is string ns &&
                !string.IsNullOrWhiteSpace(ns))
            {
                builder.Add(ns);
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// ARK001: 检查 Godot Node 派生类的字段和属性是否引用了 DTO 类型。
    /// </summary>
    private static void AnalyzeNamedType(
        SymbolAnalysisContext context,
        INamedTypeSymbol godotNodeSymbol,
        ImmutableArray<string> forbiddenPrefixes)
    {
        if (context.Symbol is not INamedTypeSymbol typeSymbol)
            return;

        if (!InheritsFrom(typeSymbol, godotNodeSymbol))
            return;

        // Check fields
        foreach (var member in typeSymbol.GetMembers())
        {
            ITypeSymbol? memberType = null;
            Location? location = null;

            switch (member)
            {
                case IFieldSymbol field:
                    memberType = field.Type;
                    location = field.Locations.FirstOrDefault();
                    break;
                case IPropertySymbol prop:
                    memberType = prop.Type;
                    location = prop.Locations.FirstOrDefault();
                    break;
            }

            if (memberType is null || location is null)
                continue;

            if (IsForbiddenDtoType(memberType, forbiddenPrefixes))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.DtoInGodotNode,
                    location,
                    memberType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    typeSymbol.Name));
            }
        }
    }

    /// <summary>
    /// ARK002: 检查 Godot Node 派生类中方法的参数、返回值是否引用了 DTO 类型。
    /// </summary>
    private static void AnalyzeMethodDeclaration(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol godotNodeSymbol,
        ImmutableArray<string> forbiddenPrefixes)
    {
        var methodSyntax = (MethodDeclarationSyntax)context.Node;
        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodSyntax);
        if (methodSymbol is null)
            return;

        var containingType = methodSymbol.ContainingType;
        if (containingType is null || !InheritsFrom(containingType, godotNodeSymbol))
            return;

        // Check return type
        if (IsForbiddenDtoType(methodSymbol.ReturnType, forbiddenPrefixes))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DtoInGodotMethod,
                methodSyntax.ReturnType.GetLocation(),
                methodSymbol.Name,
                containingType.Name,
                methodSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }

        // Check parameters
        foreach (var param in methodSymbol.Parameters)
        {
            if (IsForbiddenDtoType(param.Type, forbiddenPrefixes))
            {
                var paramSyntax = methodSyntax.ParameterList.Parameters
                    .FirstOrDefault(p => p.Identifier.ValueText == param.Name);

                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.DtoInGodotMethod,
                    paramSyntax?.Type?.GetLocation() ?? methodSyntax.GetLocation(),
                    methodSymbol.Name,
                    containingType.Name,
                    param.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
        }

        // Check local variable declarations
        var locals = methodSyntax.DescendantNodes().OfType<LocalDeclarationStatementSyntax>();
        foreach (var local in locals)
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(local.Declaration.Type);
            if (typeInfo.Type is not null && IsForbiddenDtoType(typeInfo.Type, forbiddenPrefixes))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.DtoInGodotMethod,
                    local.Declaration.Type.GetLocation(),
                    methodSymbol.Name,
                    containingType.Name,
                    typeInfo.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
        }
    }

    /// <summary>
    /// 判断类型是否属于禁止的 DTO 命名空间。
    /// 会解包泛型参数和数组元素类型。
    /// </summary>
    private static bool IsForbiddenDtoType(ITypeSymbol type, ImmutableArray<string> forbiddenPrefixes)
    {
        // Unwrap arrays
        if (type is IArrayTypeSymbol arrayType)
            return IsForbiddenDtoType(arrayType.ElementType, forbiddenPrefixes);

        // Unwrap generic type arguments (e.g. IReadOnlyList<SomeDto>)
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            foreach (var typeArg in namedType.TypeArguments)
            {
                if (IsForbiddenDtoType(typeArg, forbiddenPrefixes))
                    return true;
            }
        }

        var ns = type.ContainingNamespace?.ToDisplayString();
        if (ns is null)
            return false;

        foreach (var prefix in forbiddenPrefixes)
        {
            if (ns.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 检查 <paramref name="type"/> 是否直接或间接继承自 <paramref name="baseType"/>。
    /// </summary>
    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
            current = current.BaseType;
        }
        return false;
    }
}
