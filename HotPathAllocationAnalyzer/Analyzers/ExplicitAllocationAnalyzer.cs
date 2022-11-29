using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using ClrHeapAllocationAnalyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace HotPathAllocationAnalyzer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ExplicitAllocationAnalyzer : AllocationAnalyzer
    {
        public static DiagnosticDescriptor NewArrayRule = new DiagnosticDescriptor("HAA0501", "Explicit new array type allocation", "Explicit new array type allocation", "Performance", DiagnosticSeverity.Error, true); 
        public static DiagnosticDescriptor NewObjectRule = new DiagnosticDescriptor("HAA0502", "Explicit new reference type allocation", "Explicit new reference type allocation", "Performance", DiagnosticSeverity.Error, true); 
        public static DiagnosticDescriptor AnonymousNewObjectRule = new DiagnosticDescriptor("HAA0503", "Explicit new anonymous object allocation", "Explicit new anonymous object allocation", "Performance", DiagnosticSeverity.Error, true, string.Empty, "https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/anonymous-types"); 
        public static DiagnosticDescriptor ImplicitArrayCreationRule = new DiagnosticDescriptor("HAA0504", "Implicit new array creation allocation", "Implicit new array creation allocation", "Performance", DiagnosticSeverity.Error, true); 
        public static DiagnosticDescriptor InitializerCreationRule = new DiagnosticDescriptor("HAA0505", "Initializer reference type allocation", "Initializer reference type allocation", "Performance", DiagnosticSeverity.Error, true); 
        public static DiagnosticDescriptor LetCauseRule = new DiagnosticDescriptor("HAA0506", "Let clause induced allocation", "Let clause induced allocation", "Performance", DiagnosticSeverity.Error, true); 
        public static DiagnosticDescriptor TargetTypeNewRule = new DiagnosticDescriptor("HAA0506", "Target type new allocation", "Target type new allocation", "Performance", DiagnosticSeverity.Error, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(LetCauseRule, InitializerCreationRule, ImplicitArrayCreationRule, AnonymousNewObjectRule, NewObjectRule, NewArrayRule);

        protected override SyntaxKind[] Expressions => new[]
        {
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
            SyntaxKind.VariableDeclaration,                 // Used for target type new
        };

        private static readonly object[] EmptyMessageArgs = { };

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

            if (node is InitializerExpressionSyntax objectInitializerSyntax)
            {
                if (node.Kind() != SyntaxKind.ObjectInitializerExpression)
                    return;

                var (ancestorType, ancestor) = objectInitializerSyntax.FindAncestor(SyntaxKind.ObjectCreationExpression,
                                                                                    SyntaxKind.AnonymousObjectCreationExpression,
                                                                                    SyntaxKind.ImplicitObjectCreationExpression);
                if (ancestor == null)
                    return;

                var typeInfo = semanticModel.GetTypeInfo(ancestor, cancellationToken);
                if (typeInfo.ConvertedType != null && typeInfo.ConvertedType.TypeKind != TypeKind.Error && typeInfo.ConvertedType.IsReferenceType)
                {
                    reportDiagnostic(Diagnostic.Create(InitializerCreationRule, objectInitializerSyntax.GetLocation(), EmptyMessageArgs));
                    HeapAllocationAnalyzerEventSource.Logger.NewInitializerExpression(filePath);
                    return;
                }
            }

            if (node is ImplicitArrayCreationExpressionSyntax implicitArrayExpression)
            {
                reportDiagnostic(Diagnostic.Create(ImplicitArrayCreationRule, implicitArrayExpression.NewKeyword.GetLocation(), EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.NewImplicitArrayCreationExpression(filePath);
                return;
            }

            if (node is AnonymousObjectCreationExpressionSyntax newAnon)
            {
                reportDiagnostic(Diagnostic.Create(AnonymousNewObjectRule, newAnon.NewKeyword.GetLocation(), EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.NewAnonymousObjectCreationExpression(filePath);
                return;
            }

            if (node is ArrayCreationExpressionSyntax newArr)
            {
                reportDiagnostic(Diagnostic.Create(NewArrayRule, newArr.NewKeyword.GetLocation(), EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.NewArrayExpression(filePath);
                return;
            }

            if (node is LetClauseSyntax letKind)
            {
                reportDiagnostic(Diagnostic.Create(LetCauseRule, letKind.LetKeyword.GetLocation(), EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.LetClauseExpression(filePath);
                return;
            }

            if (node is ImplicitObjectCreationExpressionSyntax implicitObjectCreation)
            {
                AnalyzeObjectCreationSyntax(context, implicitObjectCreation, TargetTypeNewRule);
            }
        }

        private void AnalyzeObjectCreationSyntax(SyntaxNodeAnalysisContext context, SyntaxNode node, DiagnosticDescriptor diagnosticDescriptor)
        {
            if (node is not ObjectCreationExpressionSyntax && node is not ImplicitObjectCreationExpressionSyntax)
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
            
            foreach (var path in paths)
            {
                var ancestor = node.SearchPath(path.ToArray());
                if (ancestor != null)
                {
                    Diagnostic.Create(diagnosticDescriptor, ancestor.GetLocation(), EmptyMessageArgs);
                    context.ReportDiagnostic(Diagnostic.Create(diagnosticDescriptor, ancestor.GetLocation(), EmptyMessageArgs));
                    return;
                }
            }
            context.ReportDiagnostic(Diagnostic.Create(diagnosticDescriptor, node.GetLocation(), EmptyMessageArgs));

        }

        private bool IsReferenceType(SyntaxNodeAnalysisContext context, SyntaxNode node)
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(node, context.CancellationToken);
            return typeInfo.ConvertedType != null && typeInfo.ConvertedType.TypeKind != TypeKind.Error && typeInfo.ConvertedType.IsReferenceType;
        }
    }
}
