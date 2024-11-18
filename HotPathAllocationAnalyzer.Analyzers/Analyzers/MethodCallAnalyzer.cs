using System;
using System.Collections.Immutable;
using System.Linq;
using HotPathAllocationAnalyzer.Helpers;
using HotPathAllocationAnalyzer.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HotPathAllocationAnalyzer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MethodCallAnalyzer : WhitelistedAnalyzer
    {
        public static readonly DiagnosticDescriptor ExternalMethodCallRule = new("HAA0701", "Unsafe method call", $"All method call from here should be marked as {nameof(NoAllocation)} or whitelisted {{0}}", "Performance", DiagnosticSeverity.Error, true);
        public static readonly DiagnosticDescriptor UnsafePropertyAccessRule = new("HAA0702", "Unsafe property access", $"All property access from here should be marked as {nameof(NoAllocation)} or whitelisted {{0}}", "Performance", DiagnosticSeverity.Error, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ExternalMethodCallRule, UnsafePropertyAccessRule);

        protected override SyntaxKind[] Expressions => [SyntaxKind.InvocationExpression, SyntaxKind.SimpleMemberAccessExpression];

        protected override void AnalyzeNode(WhitelistedAnalysisContext analysisContext, SyntaxNodeAnalysisContext context)
        {
            var semanticModel = context.SemanticModel;
            var cancellationToken = context.CancellationToken;

            if (context.Node is InvocationExpressionSyntax invocationExpression && semanticModel.GetSymbolInfo(invocationExpression, cancellationToken).Symbol is IMethodSymbol methodInfo)
            {
                if (!AttributeHelper.HasNoAllocationAttribute(methodInfo)
                    && !AttributeHelper.HasIgnoreAllocationAttribute(methodInfo)
                    && !analysisContext.IsWhitelisted(methodInfo)
                    && !IsInSafeScope(semanticModel, invocationExpression))
                {
                    ReportError(analysisContext, context, invocationExpression, MethodSymbolSerializer.Serialize(methodInfo), ExternalMethodCallRule, HeapAllocationAnalyzerEventSource.Logger.PossiblyAllocatingMethodCall);
                }
            }

            if (context.Node is MemberAccessExpressionSyntax memberAccessExpression && semanticModel.GetSymbolInfo(memberAccessExpression, cancellationToken).Symbol is IPropertySymbol propertyInfo)
            {
                if (!AttributeHelper.HasNoAllocationAttribute(propertyInfo)
                    && !AttributeHelper.HasNoAllocationAttribute(propertyInfo.GetMethod)
                    && !AttributeHelper.HasIgnoreAllocationAttribute(propertyInfo)
                    && !AttributeHelper.HasIgnoreAllocationAttribute(propertyInfo.GetMethod)
                    && !IsAutoProperty(propertyInfo)
                    && !analysisContext.IsWhitelisted(propertyInfo)
                    && !IsInSafeScope(semanticModel, memberAccessExpression))
                {
                    ReportError(analysisContext, context, memberAccessExpression, MethodSymbolSerializer.Serialize(propertyInfo), UnsafePropertyAccessRule, HeapAllocationAnalyzerEventSource.Logger.PossiblyAllocatingMethodCall);
                }
            }
        }

        private static bool IsAutoProperty(IPropertySymbol propertyInfo)
        {
            var name = propertyInfo.Name;
            var fields = propertyInfo.ContainingType.GetMembers()
                                     .Where(x => x.Name.Contains($"<{name}>"));

            return fields.Any() || (propertyInfo.GetMethod?.GetAttributes().Any(AllocationRules.IsCompilerGeneratedAttribute) ?? false);
        }

        private static bool IsInSafeScope(SemanticModel semanticModel, SyntaxNode? symbol)
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

        private static void ReportError(WhitelistedAnalysisContext analysisContext, SyntaxNodeAnalysisContext context, SyntaxNode node, string name, DiagnosticDescriptor diagnosticDescriptor, Action<string> logger)
        {
            var details = $"[{node} / {name}]";
            if (analysisContext.HasWhitelist)
                details += " (no whitelist found)";
            else if (analysisContext.IsEmptyWhitelist)
                details += " (empty whitelist)";

            context.ReportDiagnostic(Diagnostic.Create(diagnosticDescriptor, node.GetLocation(), details));
            logger.Invoke(node.SyntaxTree.FilePath);
        }
    }
}
