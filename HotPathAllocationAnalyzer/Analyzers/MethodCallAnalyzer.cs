using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
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
    public class MethodCallAnalyzer : AllocationAnalyzer
    {
        private readonly HashSet<string> _whitelistedMethods = new();
        
        public static readonly DiagnosticDescriptor ExternalMethodCallRule = new("HAA0701", "Unsafe method call", $"All method call from here should be marked as {nameof(NoAllocation)} or whitelisted {{0}}", "Performance", DiagnosticSeverity.Error, true);
        public static readonly DiagnosticDescriptor UnsafePropertyAccessRule = new("HAA0702", "Unsafe property access", $"All property access from here should be marked as {nameof(NoAllocation)} or whitelisted {{0}}", "Performance", DiagnosticSeverity.Error, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ExternalMethodCallRule, UnsafePropertyAccessRule);
        
        protected override SyntaxKind[] Expressions => new[] { SyntaxKind.InvocationExpression, SyntaxKind.SimpleMemberAccessExpression };
        
        private static readonly object[] EmptyMessageArgs = { };

        public void AddToWhiteList(string method)
        {
            _whitelistedMethods.Add(method);
        }

        public override void Initialize(AnalysisContext context)
        {
            base.Initialize(context);

            context.RegisterCompilationStartAction(analysisContext =>
            {
                _whitelistedMethods.UnionWith(GetWhiteListedSymbols(analysisContext));
            });
        }

        protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var semanticModel = context.SemanticModel;
            var cancellationToken = context.CancellationToken;

            if (context.Node is InvocationExpressionSyntax invocationExpression && semanticModel.GetSymbolInfo(invocationExpression, cancellationToken).Symbol is IMethodSymbol methodInfo)
            {
                if (!AttributeHelper.HasNoAllocationAttribute(methodInfo)
                    && !AttributeHelper.HasIgnoreAllocationAttribute(methodInfo)
                    && !IsWhitelisted(methodInfo)
                    && !IsInSafeScope(semanticModel, invocationExpression))
                {
                    ReportError(context, invocationExpression, MethodSymbolSerializer.Serialize(methodInfo), ExternalMethodCallRule);
                }
            }

            if (context.Node is MemberAccessExpressionSyntax memberAccessExpression && semanticModel.GetSymbolInfo(memberAccessExpression, cancellationToken).Symbol is IPropertySymbol propertyInfo)
            {
                if (!AttributeHelper.HasNoAllocationAttribute(propertyInfo)
                    && !AttributeHelper.HasNoAllocationAttribute(propertyInfo.GetMethod)
                    && !AttributeHelper.HasIgnoreAllocationAttribute(propertyInfo)
                    && !AttributeHelper.HasIgnoreAllocationAttribute(propertyInfo.GetMethod)
                    && !IsAutoProperty(context, propertyInfo)
                    && !IsWhitelisted(propertyInfo)
                    && !IsInSafeScope(semanticModel, memberAccessExpression))
                {
                    ReportError(context, memberAccessExpression, MethodSymbolSerializer.Serialize(propertyInfo), UnsafePropertyAccessRule);
                }
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

        private static bool IsAutoProperty(SyntaxNodeAnalysisContext context, IPropertySymbol propertyInfo)
        {
            var name = propertyInfo.Name;
            var fields = propertyInfo.ContainingType.GetMembers()
                                     .Where(x => x.Name.Contains($"<{name}>"));
            
            return fields.Any() || (propertyInfo.GetMethod?.GetAttributes().Any(AllocationRules.IsCompilerGeneratedAttribute) ?? false);
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

        private void ReportError(SyntaxNodeAnalysisContext context, SyntaxNode node, string name, DiagnosticDescriptor externalMethodCallRule)
        {
            var details = $"[{node} / {name}]";
            if (!_whitelistFound)
                details += " (no whitelist found)";
            else if (_whitelistedMethods.Count == 0)
                details += $" (empty whitelist')";
            
            context.ReportDiagnostic(Diagnostic.Create(externalMethodCallRule, node.GetLocation(), details));
            HeapAllocationAnalyzerEventSource.Logger.PossiblyAllocatingMethodCall(node.SyntaxTree.FilePath);
        }
    }
}
