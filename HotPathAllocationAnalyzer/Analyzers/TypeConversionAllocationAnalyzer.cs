using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HotPathAllocationAnalyzer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class TypeConversionAllocationAnalyzer : AllocationAnalyzer
    {
        public static DiagnosticDescriptor ValueTypeToReferenceTypeConversionRule = new DiagnosticDescriptor("HAA0601", "Value type to reference type conversion causing boxing allocation", "Value type to reference type conversion causes boxing at call site (here), and unboxing at the callee-site. Consider using generics if applicable", "Performance", DiagnosticSeverity.Error, true);

        public static DiagnosticDescriptor DelegateOnStructInstanceRule = new DiagnosticDescriptor("HAA0602", "Delegate on struct instance caused a boxing allocation", "Struct instance method being used for delegate creation, this will result in a boxing instruction", "Performance", DiagnosticSeverity.Error, true);

        public static DiagnosticDescriptor MethodGroupAllocationRule = new DiagnosticDescriptor("HAA0603", "Delegate allocation from a method group", "This will allocate a delegate instance", "Performance", DiagnosticSeverity.Error, true);

        public static DiagnosticDescriptor ReadonlyMethodGroupAllocationRule = new DiagnosticDescriptor("HAA0604", "Delegate allocation from a method group", "This will allocate a delegate instance", "Performance", DiagnosticSeverity.Error, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ValueTypeToReferenceTypeConversionRule, DelegateOnStructInstanceRule, MethodGroupAllocationRule, ReadonlyMethodGroupAllocationRule);

        protected override SyntaxKind[] Expressions => new[]
        {
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxKind.ReturnStatement,
            SyntaxKind.YieldReturnStatement,
            SyntaxKind.CastExpression,
            SyntaxKind.AsExpression,
            SyntaxKind.CoalesceExpression,
            SyntaxKind.ConditionalExpression,
            SyntaxKind.ForEachStatement,
            SyntaxKind.EqualsValueClause,
            SyntaxKind.Argument,
            SyntaxKind.ArrowExpressionClause,
            SyntaxKind.Interpolation
        };

        private static readonly object[] EmptyMessageArgs = { };

        public TypeConversionAllocationAnalyzer()
        {
        }

        public TypeConversionAllocationAnalyzer(bool forceAnalysis) : base(forceAnalysis)
        {
        }

        protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var node = context.Node;
            var semanticModel = context.SemanticModel;
            var cancellationToken = context.CancellationToken;
            Action<Diagnostic> reportDiagnostic = context.ReportDiagnostic;
            string filePath = node.SyntaxTree.FilePath;
            bool assignedToReadonlyFieldOrProperty = 
                (context.ContainingSymbol as IFieldSymbol)?.IsReadOnly == true ||
                (context.ContainingSymbol as IPropertySymbol)?.IsReadOnly == true;

            // this.fooObjCall(10);
            // new myobject(10);
            if (node is ArgumentSyntax argumentSyntax)
            {
                ArgumentSyntaxCheck(argumentSyntax, semanticModel, assignedToReadonlyFieldOrProperty, reportDiagnostic, filePath, cancellationToken);
            }

            // object foo { get { return 0; } }
            if (node is ReturnStatementSyntax returnStatementSyntax)
            {
                ReturnStatementExpressionCheck(returnStatementSyntax, semanticModel, reportDiagnostic, filePath, cancellationToken);
                return;
            }

            // yield return 0
            if (node is YieldStatementSyntax yieldStatementSyntax)
            {
                YieldReturnStatementExpressionCheck(yieldStatementSyntax, semanticModel, reportDiagnostic, filePath, cancellationToken);
                return;
            }

            // object a = x ?? 0;
            // var a = 10 as object;
            if (node is BinaryExpressionSyntax binaryExpressionSyntax)
            {
                BinaryExpressionCheck(binaryExpressionSyntax, semanticModel, assignedToReadonlyFieldOrProperty, reportDiagnostic, filePath, cancellationToken);
                return;
            }

            // for (object i = 0;;)
            if (node is EqualsValueClauseSyntax equalsValueClauseSyntax)
            {
                EqualsValueClauseCheck(equalsValueClauseSyntax, semanticModel, assignedToReadonlyFieldOrProperty, reportDiagnostic, filePath, cancellationToken);
                return;
            }

            // object = true ? 0 : obj
            if (node is ConditionalExpressionSyntax conditionalExpressionSyntax)
            {
                ConditionalExpressionCheck(conditionalExpressionSyntax, semanticModel, reportDiagnostic, filePath, cancellationToken);
                return;
            }

            // string a = $"{1}";
            if (node is InterpolationSyntax interpolationSyntax) {
                InterpolationCheck(interpolationSyntax, semanticModel, reportDiagnostic, filePath, cancellationToken);
                return;
            }

            // var f = (object)
            if (node is CastExpressionSyntax castExpressionSyntax)
            {
                CastExpressionCheck(castExpressionSyntax, semanticModel, reportDiagnostic, filePath, cancellationToken);
                return;
            }

            // object Foo => 1
            if (node is ArrowExpressionClauseSyntax arrowExpressionClauseSyntax)
            {
                ArrowExpressionCheck(arrowExpressionClauseSyntax, semanticModel, assignedToReadonlyFieldOrProperty, reportDiagnostic, filePath, cancellationToken);
                return;
            }
        }

        private static void ReturnStatementExpressionCheck(ReturnStatementSyntax returnStatementExpression, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath, CancellationToken cancellationToken)
        {
            if (returnStatementExpression.Expression != null)
            {
                var returnConversionInfo = semanticModel.GetConversion(returnStatementExpression.Expression, cancellationToken);
                CheckTypeConversion(returnConversionInfo, reportDiagnostic, returnStatementExpression.Expression.GetLocation(), filePath);
            }
        }

        private static void YieldReturnStatementExpressionCheck(YieldStatementSyntax yieldExpression, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath, CancellationToken cancellationToken)
        {
            if (yieldExpression.Expression != null)
            {
                var returnConversionInfo = semanticModel.GetConversion(yieldExpression.Expression, cancellationToken);
                CheckTypeConversion(returnConversionInfo, reportDiagnostic, yieldExpression.Expression.GetLocation(), filePath);
            }
        }

        private static void ArgumentSyntaxCheck(ArgumentSyntax argument, SemanticModel semanticModel, bool isAssignmentToReadonly, Action<Diagnostic> reportDiagnostic, string filePath, CancellationToken cancellationToken)
        {
            if (argument.Expression != null)
            {
                var argumentTypeInfo = semanticModel.GetTypeInfo(argument.Expression, cancellationToken);
                var argumentConversionInfo = semanticModel.GetConversion(argument.Expression, cancellationToken);
                CheckTypeConversion(argumentConversionInfo, reportDiagnostic, argument.Expression.GetLocation(), filePath);
                CheckDelegateCreation(argument.Expression, argumentTypeInfo, semanticModel, isAssignmentToReadonly, reportDiagnostic, argument.Expression.GetLocation(), filePath, cancellationToken);
            }
        }

        private static void BinaryExpressionCheck(BinaryExpressionSyntax binaryExpression, SemanticModel semanticModel, bool isAssignmentToReadonly, Action<Diagnostic> reportDiagnostic, string filePath, CancellationToken cancellationToken)
        {
            // as expression
            if (binaryExpression.IsKind(SyntaxKind.AsExpression) && binaryExpression.Left != null && binaryExpression.Right != null)
            {
                var leftT = semanticModel.GetTypeInfo(binaryExpression.Left, cancellationToken);
                var rightT = semanticModel.GetTypeInfo(binaryExpression.Right, cancellationToken);

                if (leftT.Type?.IsValueType == true && rightT.Type?.IsReferenceType == true)
                {
                    reportDiagnostic(Diagnostic.Create(ValueTypeToReferenceTypeConversionRule, binaryExpression.Left.GetLocation(), EmptyMessageArgs));
                    HeapAllocationAnalyzerEventSource.Logger.BoxingAllocation(filePath);
                }

                return;
            }

            if (binaryExpression.Right != null)
            {
                var assignmentExprTypeInfo = semanticModel.GetTypeInfo(binaryExpression.Right, cancellationToken);
                var assignmentExprConversionInfo = semanticModel.GetConversion(binaryExpression.Right, cancellationToken);
                CheckTypeConversion(assignmentExprConversionInfo, reportDiagnostic, binaryExpression.Right.GetLocation(), filePath);
                CheckDelegateCreation(binaryExpression.Right, assignmentExprTypeInfo, semanticModel, isAssignmentToReadonly, reportDiagnostic, binaryExpression.Right.GetLocation(), filePath, cancellationToken);
                return;
            }
        }

        private static void InterpolationCheck(InterpolationSyntax interpolation, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath, CancellationToken cancellationToken)
        {
            var typeInfo = semanticModel.GetTypeInfo(interpolation.Expression, cancellationToken);
            if (typeInfo.Type?.IsValueType == true) {
                reportDiagnostic(Diagnostic.Create(ValueTypeToReferenceTypeConversionRule, interpolation.Expression.GetLocation(), EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.BoxingAllocation(filePath);
            }
        }

        private static void CastExpressionCheck(CastExpressionSyntax castExpression, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath, CancellationToken cancellationToken)
        {
            if (castExpression.Expression != null)
            {
                var castTypeInfo = semanticModel.GetTypeInfo(castExpression, cancellationToken);
                var expressionTypeInfo = semanticModel.GetTypeInfo(castExpression.Expression, cancellationToken);

                if (castTypeInfo.Type?.IsReferenceType == true && expressionTypeInfo.Type?.IsValueType == true)
                {
                    reportDiagnostic(Diagnostic.Create(ValueTypeToReferenceTypeConversionRule, castExpression.Expression.GetLocation(), EmptyMessageArgs));
                }
            }
        }

        private static void ConditionalExpressionCheck(ConditionalExpressionSyntax conditionalExpression, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath, CancellationToken cancellationToken)
        {
            var trueExp = conditionalExpression.WhenTrue;
            var falseExp = conditionalExpression.WhenFalse;

            if (trueExp != null)
            {
                CheckTypeConversion(semanticModel.GetConversion(trueExp, cancellationToken), reportDiagnostic, trueExp.GetLocation(), filePath);
            }

            if (falseExp != null)
            {
                CheckTypeConversion(semanticModel.GetConversion(falseExp, cancellationToken), reportDiagnostic, falseExp.GetLocation(), filePath);
            }
        }

        private static void EqualsValueClauseCheck(EqualsValueClauseSyntax initializer, SemanticModel semanticModel, bool isAssignmentToReadonly, Action<Diagnostic> reportDiagnostic, string filePath, CancellationToken cancellationToken)
        {
            if (initializer.Value != null)
            {
                var typeInfo = semanticModel.GetTypeInfo(initializer.Value, cancellationToken);
                var conversionInfo = semanticModel.GetConversion(initializer.Value, cancellationToken);
                CheckTypeConversion(conversionInfo, reportDiagnostic, initializer.Value.GetLocation(), filePath);
                CheckDelegateCreation(initializer.Value, typeInfo, semanticModel, isAssignmentToReadonly, reportDiagnostic, initializer.Value.GetLocation(), filePath, cancellationToken);
            }
        }


        private static void ArrowExpressionCheck(ArrowExpressionClauseSyntax syntax, SemanticModel semanticModel, bool isAssignmentToReadonly, Action<Diagnostic> reportDiagnostic, string filePath, CancellationToken cancellationToken)
        {
            var typeInfo = semanticModel.GetTypeInfo(syntax.Expression, cancellationToken);
            var conversionInfo = semanticModel.GetConversion(syntax.Expression, cancellationToken);
            CheckTypeConversion(conversionInfo, reportDiagnostic, syntax.Expression.GetLocation(), filePath);
            CheckDelegateCreation(syntax, typeInfo, semanticModel, false, reportDiagnostic,
                syntax.Expression.GetLocation(), filePath, cancellationToken);
        }

        private static void CheckTypeConversion(Conversion conversionInfo, Action<Diagnostic> reportDiagnostic, Location location, string filePath)
        {
            if (conversionInfo.IsBoxing)
            {
                reportDiagnostic(Diagnostic.Create(ValueTypeToReferenceTypeConversionRule, location, EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.BoxingAllocation(filePath);
            }
        }

        private static void CheckDelegateCreation(SyntaxNode node, TypeInfo typeInfo, SemanticModel semanticModel, bool isAssignmentToReadonly, Action<Diagnostic> reportDiagnostic, Location location, string filePath, CancellationToken cancellationToken)
        {
            // special case: method groups
            if (typeInfo.ConvertedType?.TypeKind == TypeKind.Delegate)
            {
                // new Action<Foo>(MethodGroup); should skip this one
                var insideObjectCreation = node.Parent?.Parent?.Parent?.Kind() == SyntaxKind.ObjectCreationExpression;
                if (node is ParenthesizedLambdaExpressionSyntax || node is SimpleLambdaExpressionSyntax ||
                    node is AnonymousMethodExpressionSyntax || node is ObjectCreationExpressionSyntax ||
                    insideObjectCreation)
                {
                    // skip this, because it's intended.
                }
                else
                {
                    if (node.IsKind(SyntaxKind.IdentifierName))
                    {
                        if (semanticModel.GetSymbolInfo(node, cancellationToken).Symbol is IMethodSymbol) {
                            reportDiagnostic(Diagnostic.Create(MethodGroupAllocationRule, location, EmptyMessageArgs));
                            HeapAllocationAnalyzerEventSource.Logger.MethodGroupAllocation(filePath);
                        }
                    }
                    else if (node.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    {
                        if (node is MemberAccessExpressionSyntax memberAccess && semanticModel.GetSymbolInfo(memberAccess.Name, cancellationToken).Symbol is IMethodSymbol)
                        {
                            if (isAssignmentToReadonly)
                            {
                                reportDiagnostic(Diagnostic.Create(ReadonlyMethodGroupAllocationRule, location, EmptyMessageArgs));
                                HeapAllocationAnalyzerEventSource.Logger.ReadonlyMethodGroupAllocation(filePath);
                            }
                            else
                            {
                                reportDiagnostic(Diagnostic.Create(MethodGroupAllocationRule, location, EmptyMessageArgs));
                                HeapAllocationAnalyzerEventSource.Logger.MethodGroupAllocation(filePath);
                            }
                        }
                    } 
                    else if (node is ArrowExpressionClauseSyntax arrowClause)
                    {
                        if (semanticModel.GetSymbolInfo(arrowClause.Expression, cancellationToken).Symbol is IMethodSymbol) {
                            reportDiagnostic(Diagnostic.Create(MethodGroupAllocationRule, location, EmptyMessageArgs));
                            HeapAllocationAnalyzerEventSource.Logger.MethodGroupAllocation(filePath);
                        }
                    }
                }

                if (IsStructInstanceMethod(node, semanticModel, cancellationToken))
                {
                    reportDiagnostic(Diagnostic.Create(DelegateOnStructInstanceRule, location, EmptyMessageArgs));
                }
            }
        }

        private static bool IsStructInstanceMethod(SyntaxNode node, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            var nodeKind = node.Kind();
            if (nodeKind == SyntaxKind.AnonymousMethodExpression || nodeKind == SyntaxKind.ParenthesizedLambdaExpression)
            {
                return false;
            }

            var symbolInfo = semanticModel.GetSymbolInfo(node, cancellationToken).Symbol;
            return symbolInfo != null && symbolInfo.Kind == SymbolKind.Method && symbolInfo.IsStatic == false && symbolInfo.ContainingType?.IsValueType == true;
        }
    }
}
