using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ClrHeapAllocationAnalyzer
{
    
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MethodCallAnalyzer : AllocationAnalyzer
    {
        public static DiagnosticDescriptor ExternalMethodCallRule = new DiagnosticDescriptor("HAA0701", "Unsafe method call", "All method call from here should be marked as RestrictedAllocation or whitelisted", "Performance", DiagnosticSeverity.Warning, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ExternalMethodCallRule);
        protected override SyntaxKind[] Expressions => new[] { SyntaxKind.InvocationExpression };
        private static readonly object[] EmptyMessageArgs = { };

        protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var invocationExpression = context.Node as InvocationExpressionSyntax;
            var semanticModel = context.SemanticModel;
            var cancellationToken = context.CancellationToken;
            
            if (semanticModel.GetSymbolInfo(invocationExpression, cancellationToken).Symbol is IMethodSymbol methodInfo)
            {
                if (!HasRestrictedAllocationAttribute(methodInfo) && !IsInSafeScope(semanticModel, invocationExpression))
                    ReportError(context, invocationExpression);
            }
        }

        private static bool IsInSafeScope(SemanticModel semanticModel, SyntaxNode symbol)
        {
            if (symbol == null)
                return false;
            
            if (symbol.Parent is UsingStatementSyntax usingStatement && usingStatement.Expression is ObjectCreationExpressionSyntax creationExpressionSyntax)
            {
                var type = semanticModel.GetTypeInfo(creationExpressionSyntax).Type;
                if (IsSafeScopeType(type))
                    return true;
            }
            
            if (symbol.Parent is BlockSyntax blockSyntax)
            {
                var usingStatements = blockSyntax.Statements
                                                 .TakeWhile(x => !x.Equals(symbol))
                                                 .OfType<LocalDeclarationStatementSyntax>()
                                                 .Where(x => x.UsingKeyword != null)
                                                 .Select(x => semanticModel.GetTypeInfo(x.Declaration.Type).Type)
                                                 .ToArray();
                
                if (usingStatements.Any(IsSafeScopeType))
                    return true;
            }

            return IsInSafeScope(semanticModel, symbol.Parent);
        }

        private static bool IsSafeScopeType(ITypeSymbol type)
        {
            return type.Name == nameof(AllocationFreeScope) && type.ContainingNamespace.Name == typeof(AllocationFreeScope).Namespace;
        }

        private static void ReportError(SyntaxNodeAnalysisContext context, SyntaxNode node)
        {
            context.ReportDiagnostic(Diagnostic.Create(ExternalMethodCallRule, node.GetLocation(), EmptyMessageArgs));
            HeapAllocationAnalyzerEventSource.Logger.PossiblyAllocatingMethodCall(node.SyntaxTree.FilePath);
        }
    }
}
