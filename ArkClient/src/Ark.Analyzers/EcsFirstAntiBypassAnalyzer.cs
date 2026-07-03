using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ark.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EcsFirstAntiBypassAnalyzer : DiagnosticAnalyzer
{
    private const string SuppressMessageAttributeMetadataName = "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.EcsAnalyzerBypass);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxTreeAction(AnalyzePragmaSuppressions);
        context.RegisterSyntaxNodeAction(AnalyzeSuppressMessageAttributes, SyntaxKind.Attribute);
    }

    private static void AnalyzePragmaSuppressions(SyntaxTreeAnalysisContext context)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);
        foreach (var trivia in root.DescendantTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia))
                continue;

            var pragma = (PragmaWarningDirectiveTriviaSyntax)trivia.GetStructure()!;
            if (!pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
                continue;

            foreach (var errorCode in pragma.ErrorCodes)
            {
                var code = errorCode.ToString().Trim();
                if (!code.StartsWith("ECS", StringComparison.OrdinalIgnoreCase))
                    continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.EcsAnalyzerBypass,
                    errorCode.GetLocation(),
                    code));
            }
        }
    }

    private static void AnalyzeSuppressMessageAttributes(SyntaxNodeAnalysisContext context)
    {
        var attributeSyntax = (AttributeSyntax)context.Node;
        var attributeSymbol = context.SemanticModel.GetSymbolInfo(attributeSyntax, context.CancellationToken).Symbol as IMethodSymbol;
        if (attributeSymbol is null)
            return;

        var attributeType = attributeSymbol.ContainingType;
        if (!string.Equals(attributeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                "global::" + SuppressMessageAttributeMetadataName,
                StringComparison.Ordinal))
        {
            return;
        }

        if (attributeSyntax.ArgumentList is null)
            return;

        foreach (var argument in attributeSyntax.ArgumentList.Arguments)
        {
            var constant = context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken);
            if (!constant.HasValue || constant.Value is not string text)
                continue;

            if (!text.Contains("ECS", StringComparison.OrdinalIgnoreCase))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.EcsAnalyzerBypass,
                argument.GetLocation(),
                text));
            return;
        }
    }
}
