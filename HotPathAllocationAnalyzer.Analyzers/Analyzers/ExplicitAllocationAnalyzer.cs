using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HotPathAllocationAnalyzer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ExplicitAllocationAnalyzer : SyntaxNodeAllocationAnalyzer
    {
        public static readonly DiagnosticDescriptor NewArrayRule = new("HAA0501", "Explicit new array type allocation", "Explicit new array type allocation", "Performance", DiagnosticSeverity.Error, true);
        public static readonly DiagnosticDescriptor NewObjectRule = new("HAA0502", "Explicit new reference type allocation", "Explicit new reference type allocation", "Performance", DiagnosticSeverity.Error, true);
        public static readonly DiagnosticDescriptor AnonymousNewObjectRule = new("HAA0503", "Explicit new anonymous object allocation", "Explicit new anonymous object allocation", "Performance", DiagnosticSeverity.Error, true, string.Empty, "https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/anonymous-types");
        public static readonly DiagnosticDescriptor ImplicitArrayCreationRule = new("HAA0504", "Implicit new array creation allocation", "Implicit new array creation allocation", "Performance", DiagnosticSeverity.Error, true);
        public static readonly DiagnosticDescriptor InitializerCreationRule = new("HAA0505", "Initializer reference type allocation", "Initializer reference type allocation", "Performance", DiagnosticSeverity.Error, true);
        public static readonly DiagnosticDescriptor LetCauseRule = new("HAA0506", "Let clause induced allocation", "Let clause induced allocation", "Performance", DiagnosticSeverity.Error, true);
        public static readonly DiagnosticDescriptor TargetTypeNewRule = new("HAA0506", "Target type new allocation", "Target type new allocation", "Performance", DiagnosticSeverity.Error, true);
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(LetCauseRule, InitializerCreationRule, ImplicitArrayCreationRule, AnonymousNewObjectRule, NewObjectRule, NewArrayRule);

        protected override SyntaxKind[] Expressions =>
        [
            SyntaxKind.ObjectCreationExpression,            // Used
            SyntaxKind.AnonymousObjectCreationExpression,   // Used
            SyntaxKind.ArrayInitializerExpression,          // Used (this is inside an ImplicitArrayCreationExpression)
            SyntaxKind.CollectionInitializerExpression,     // Is this used anywhere?
            SyntaxKind.ComplexElementInitializerExpression, // Is this used anywhere? For what this is see http://source.roslyn.codeplex.com/#Microsoft.CodeAnalysis.CSharp/Compilation/CSharpSemanticModel.cs,80
            SyntaxKind.ObjectInitializerExpression,         // Used linked to InitializerExpressionSyntax
            SyntaxKind.ArrayCreationExpression,             // Used
            SyntaxKind.ImplicitArrayCreationExpression,     // Used (this then contains an ArrayInitializerExpression)
            SyntaxKind.LetClause,                           // Used
            SyntaxKind.ImplicitObjectCreationExpression,    // Used for target type new
            SyntaxKind.InvocationExpression,                // Used for target type new
            SyntaxKind.VariableDeclaration // Used for target type new
        ];

        public ExplicitAllocationAnalyzer()
        {
        }

        public ExplicitAllocationAnalyzer(bool forceAnalysis)
            : base(forceAnalysis)
        {
        }

        protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var node = context.Node;
            var semanticModel = context.SemanticModel;
            var reportDiagnostic = context.ReportDiagnostic;
            var cancellationToken = context.CancellationToken;
            var filePath = node.SyntaxTree.FilePath;

            if (node is ObjectCreationExpressionSyntax newObj)
            {
                AnalyzeObjectCreationSyntax(context, node, NewObjectRule);
            }

            object[] emptyMessageArgs = [];
            if (node is InitializerExpressionSyntax objectInitializerSyntax)
            {
                if (!node.IsKind(SyntaxKind.ObjectInitializerExpression))
                    return;

                var (ancestorType, ancestor) = objectInitializerSyntax.FindAncestor(SyntaxKind.ObjectCreationExpression,
                                                                                    SyntaxKind.AnonymousObjectCreationExpression,
                                                                                    SyntaxKind.ImplicitObjectCreationExpression);
                if (ancestor == null)
                    return;

                var typeInfo = semanticModel.GetTypeInfo(ancestor, cancellationToken);
                if (typeInfo.ConvertedType != null && typeInfo.ConvertedType.TypeKind != TypeKind.Error && typeInfo.ConvertedType.IsReferenceType)
                {
                    reportDiagnostic(Diagnostic.Create(InitializerCreationRule, objectInitializerSyntax.GetLocation(), emptyMessageArgs));
                    HeapAllocationAnalyzerEventSource.Logger.NewInitializerExpression(filePath);
                    return;
                }
            }

            if (node is ImplicitArrayCreationExpressionSyntax implicitArrayExpression)
            {
                reportDiagnostic(Diagnostic.Create(ImplicitArrayCreationRule, implicitArrayExpression.NewKeyword.GetLocation(), emptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.NewImplicitArrayCreationExpression(filePath);
                return;
            }

            if (node is AnonymousObjectCreationExpressionSyntax newAnon)
            {
                reportDiagnostic(Diagnostic.Create(AnonymousNewObjectRule, newAnon.NewKeyword.GetLocation(), emptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.NewAnonymousObjectCreationExpression(filePath);
                return;
            }

            if (node is ArrayCreationExpressionSyntax newArr)
            {
                reportDiagnostic(Diagnostic.Create(NewArrayRule, newArr.NewKeyword.GetLocation(), emptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.NewArrayExpression(filePath);
                return;
            }

            if (node is LetClauseSyntax letKind)
            {
                reportDiagnostic(Diagnostic.Create(LetCauseRule, letKind.LetKeyword.GetLocation(), emptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.LetClauseExpression(filePath);
                return;
            }

            if (node is ImplicitObjectCreationExpressionSyntax implicitObjectCreation)
            {
                AnalyzeObjectCreationSyntax(context, implicitObjectCreation, TargetTypeNewRule);
            }
        }

        private bool IsException(ITypeSymbol? symbol)
        {
            var current = symbol;
            while (current != null)
            {
                //use ToString to get the full name with the namespace
                if (current.ToString() == typeof(Exception).FullName)
                    return true;
                current = current.BaseType;
            }

            return false;
        }

        private void AnalyzeObjectCreationSyntax(SyntaxNodeAnalysisContext context, SyntaxNode node, DiagnosticDescriptor diagnosticDescriptor)
        {
            if (node is not ObjectCreationExpressionSyntax && node is not ImplicitObjectCreationExpressionSyntax)
                return;

            //ignore exceptions allocations
            var typeInfo = context.SemanticModel.GetTypeInfo(node, context.CancellationToken);
            if (IsException(typeInfo.Type))
                return;
            
            if (!IsReferenceType(context, node))
                return;

            //These paths are multiple scenarios to have nicer error messages
            //If we don't match any we juste display the location of the node
            var paths = new List<List<SyntaxKind>>
            {
                new() {SyntaxKind.EqualsValueClause, SyntaxKind.VariableDeclarator, SyntaxKind.VariableDeclaration}, //variableDeclarator,
                new() {SyntaxKind.Argument, SyntaxKind.ArgumentList, SyntaxKind.InvocationExpression}, // method call,
                new() {SyntaxKind.Argument, SyntaxKind.TupleExpression}, //tuple creation
                new() {SyntaxKind.ArrowExpressionClause, SyntaxKind.PropertyDeclaration}, // property declaration
                new() {SyntaxKind.ArrowExpressionClause},
                new() {SyntaxKind.ReturnStatement}
            };

            object[] emptyMessageArgs = [];
            foreach (var path in paths)
            {
                var ancestor = node.SearchPath(path.ToArray());
                if (ancestor != null)
                {
                    Diagnostic.Create(diagnosticDescriptor, ancestor.GetLocation(), emptyMessageArgs);
                    context.ReportDiagnostic(Diagnostic.Create(diagnosticDescriptor, ancestor.GetLocation(), emptyMessageArgs));
                    return;
                }
            }
            context.ReportDiagnostic(Diagnostic.Create(diagnosticDescriptor, node.GetLocation(), emptyMessageArgs));

        }

        private bool IsReferenceType(SyntaxNodeAnalysisContext context, SyntaxNode node)
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(node, context.CancellationToken);
            return typeInfo.ConvertedType != null && typeInfo.ConvertedType.TypeKind != TypeKind.Error && typeInfo.ConvertedType.IsReferenceType;
        }
    }
}
