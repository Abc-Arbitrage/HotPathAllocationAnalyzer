using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HotPathAllocationAnalyzer.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class StringInterpolationAnalyzer : WhitelistedAnalyzer
{
    public StringInterpolationAnalyzer()
    {
    }

    public StringInterpolationAnalyzer(bool forceAnalysis)
        : base(forceAnalysis)
    {
    }

    public static readonly DiagnosticDescriptor InterpolatedStringRule = new("HAA801", "String interpolation allocation", "Please use a custom handler for the string interpolation to avoid allocations", "Performance", DiagnosticSeverity.Error, true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(InterpolatedStringRule);

    protected override SyntaxKind[] Expressions { get; } =
    [
        SyntaxKind.InterpolatedStringExpression
    ];

    protected override void AnalyzeNode(WhitelistedAnalysisContext analysisContext, SyntaxNodeAnalysisContext context)
    {
        var node = context.Node;
        var semanticModel = context.SemanticModel;
        var reportDiagnostic = context.ReportDiagnostic;

        if (node is not InterpolatedStringExpressionSyntax interpolation)
            return;
        object[] emptyMessageArgs = [];
        if (node.Parent is not ArgumentSyntax)
        {
            reportDiagnostic(Diagnostic.Create(InterpolatedStringRule, interpolation.GetLocation(), emptyMessageArgs));
            return;
        }
            
        var typeInfo = semanticModel.GetTypeInfo(node);
        var typeName = typeInfo.ConvertedType?.ToString();
        if (typeName != null && analysisContext.IsWhitelisted(typeName))
            return;
        reportDiagnostic(Diagnostic.Create(InterpolatedStringRule, interpolation.GetLocation(), emptyMessageArgs));
    }
}
