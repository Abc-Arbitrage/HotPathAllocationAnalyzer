using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ClrHeapAllocationAnalyzer.Helpers;
using ClrHeapAllocationAnalyzer.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ClrHeapAllocationAnalyzer.Analyzers
{
    
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MethodCallAnalyzer : AllocationAnalyzer
    {
        private readonly HashSet<string> _whitelistedMethods = new HashSet<string>();
        
        public static DiagnosticDescriptor ExternalMethodCallRule = new DiagnosticDescriptor("HAA0701", "Unsafe method call", "All method call from here should be marked as RestrictedAllocation or whitelisted", "Performance", DiagnosticSeverity.Error, true);
        public static DiagnosticDescriptor UnsafePropertyAccessRule = new DiagnosticDescriptor("HAA0702", "Unsafe property access", "All property access from here should be marked as RestrictedAllocation or whitelisted", "Performance", DiagnosticSeverity.Error, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ExternalMethodCallRule, UnsafePropertyAccessRule);
        
        protected override SyntaxKind[] Expressions => new[] { SyntaxKind.InvocationExpression, SyntaxKind.SimpleMemberAccessExpression };
        
        private static readonly object[] EmptyMessageArgs = { };

        public override void AddToWhiteList(string method)
        {
            _whitelistedMethods.Add(method);
        }

        protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var semanticModel = context.SemanticModel;
            var cancellationToken = context.CancellationToken;

            if (context.Node is InvocationExpressionSyntax invocationExpression && semanticModel.GetSymbolInfo(invocationExpression, cancellationToken).Symbol is IMethodSymbol methodInfo)
            {
                if (!RestrictedAllocationAttributeHelper.HasRestrictedAllocationAttribute(methodInfo) && !IsWhitelisted(methodInfo) && !IsInSafeScope(semanticModel, invocationExpression))
                    ReportError(context, invocationExpression, ExternalMethodCallRule);
            }

            if (context.Node is MemberAccessExpressionSyntax memberAccessExpression && semanticModel.GetSymbolInfo(memberAccessExpression, cancellationToken).Symbol is IPropertySymbol propertyInfo)
            {
                if (!RestrictedAllocationAttributeHelper.HasRestrictedAllocationAttribute(propertyInfo) && !RestrictedAllocationAttributeHelper.HasRestrictedAllocationAttribute(propertyInfo.GetMethod) && !IsWhitelisted(propertyInfo) && !IsInSafeScope(semanticModel, memberAccessExpression))
                    ReportError(context, memberAccessExpression, UnsafePropertyAccessRule);
            }
        }

        private bool IsWhitelisted(IMethodSymbol methodInfo)
        {
            return _whitelistedMethods.Contains(MethodSymbolSerializer.Serialize(methodInfo));
        }
        
        private bool IsWhitelisted(IPropertySymbol methodInfo)
        {
            return _whitelistedMethods.Contains(MethodSymbolSerializer.Serialize(methodInfo));
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

        private static bool IsSafeScopeType(ITypeSymbol? type)
        {
            return type != null
                   && type.Name == nameof(AllocationFreeScope)
                   && type.ContainingNamespace.ToDisplayString() == typeof(AllocationFreeScope).Namespace;
        }

        private static void ReportError(SyntaxNodeAnalysisContext context, SyntaxNode node, DiagnosticDescriptor externalMethodCallRule)
        {
            context.ReportDiagnostic(Diagnostic.Create(externalMethodCallRule, node.GetLocation(), EmptyMessageArgs));
            HeapAllocationAnalyzerEventSource.Logger.PossiblyAllocatingMethodCall(node.SyntaxTree.FilePath);
        }
    }
}
