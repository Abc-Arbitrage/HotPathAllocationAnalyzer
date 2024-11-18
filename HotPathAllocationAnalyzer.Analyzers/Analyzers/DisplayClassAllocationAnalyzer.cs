using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HotPathAllocationAnalyzer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DisplayClassAllocationAnalyzer : SyntaxNodeAllocationAnalyzer
    {
        public static readonly DiagnosticDescriptor ClosureDriverRule = new("HAA0301", "Closure Allocation Source", "Heap allocation of closure Captures: {0}", "Performance", DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor ClosureCaptureRule = new("HAA0302", "Display class allocation to capture closure", "The compiler will emit a class that will hold this as a field to allow capturing of this closure", "Performance", DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor LambdaOrAnonymousMethodInGenericMethodRule = new("HAA0303", "Lambda or anonymous method in a generic method allocates a delegate instance", "Considering moving this out of the generic method", "Performance", DiagnosticSeverity.Error, true);
        
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ClosureCaptureRule, ClosureDriverRule, LambdaOrAnonymousMethodInGenericMethodRule);

        protected override SyntaxKind[] Expressions => [SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.SimpleLambdaExpression, SyntaxKind.AnonymousMethodExpression];

        public DisplayClassAllocationAnalyzer()
        {
        }

        public DisplayClassAllocationAnalyzer(bool forceAnalysis) : base(forceAnalysis)
        {
        }

        
        protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var node = context.Node;
            var semanticModel = context.SemanticModel;
            var cancellationToken = context.CancellationToken;
            Action<Diagnostic> reportDiagnostic = context.ReportDiagnostic;

            var anonExpr = node as AnonymousMethodExpressionSyntax;
            if (anonExpr?.Block?.ChildNodes() != null && anonExpr.Block.ChildNodes().Any())
            {
                GenericMethodCheck(semanticModel, node, anonExpr.DelegateKeyword.GetLocation(), reportDiagnostic, cancellationToken);
                ClosureCaptureDataFlowAnalysis(semanticModel.AnalyzeDataFlow(anonExpr.Block.ChildNodes().First(), anonExpr.Block.ChildNodes().Last()), reportDiagnostic, anonExpr.DelegateKeyword.GetLocation());
                return;
            }

            if (node is SimpleLambdaExpressionSyntax lambdaExpr)
            {
                GenericMethodCheck(semanticModel, node, lambdaExpr.ArrowToken.GetLocation(), reportDiagnostic, cancellationToken);
                ClosureCaptureDataFlowAnalysis(semanticModel.AnalyzeDataFlow(lambdaExpr), reportDiagnostic, lambdaExpr.ArrowToken.GetLocation());
                return;
            }

            if (node is ParenthesizedLambdaExpressionSyntax parenLambdaExpr)
            {
                GenericMethodCheck(semanticModel, node, parenLambdaExpr.ArrowToken.GetLocation(), reportDiagnostic, cancellationToken);
                ClosureCaptureDataFlowAnalysis(semanticModel.AnalyzeDataFlow(parenLambdaExpr), reportDiagnostic, parenLambdaExpr.ArrowToken.GetLocation());
                return;
            }
        }
        
        private static void ClosureCaptureDataFlowAnalysis(DataFlowAnalysis? flow, Action<Diagnostic> reportDiagnostic, Location location)
        {
            if (flow == null || flow.Captured.Length <= 0)
            {
                return;
            }

            foreach (var capture in flow.Captured)
            {
                if (capture.Name != null && capture.Locations != null)
                {
                    foreach (var l in capture.Locations)
                    {
                        reportDiagnostic(Diagnostic.Create(ClosureCaptureRule, l, (object[])[]));
                    }
                }
            }

            reportDiagnostic(Diagnostic.Create(ClosureDriverRule, location, new[] { string.Join(",", flow.Captured.Select(x => x.Name)) }));
        }

        private static void GenericMethodCheck(SemanticModel semanticModel, SyntaxNode node, Location location, Action<Diagnostic> reportDiagnostic, CancellationToken cancellationToken)
        {
            if (semanticModel.GetSymbolInfo(node, cancellationToken).Symbol != null)
            {
                var containingSymbol = semanticModel.GetSymbolInfo(node, cancellationToken).Symbol?.ContainingSymbol as IMethodSymbol;
                if (containingSymbol != null && containingSymbol.Arity > 0)
                {
                    reportDiagnostic(Diagnostic.Create(LambdaOrAnonymousMethodInGenericMethodRule, location, (object[])[]));
                }
            }
        }
    }
}