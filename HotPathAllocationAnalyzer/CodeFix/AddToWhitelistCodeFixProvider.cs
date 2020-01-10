using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HotPathAllocationAnalyzer.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace HotPathAllocationAnalyzer.CodeFix
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddToWhitelistCodeFixProvider)), Shared]
    public class AddToWhitelistCodeFixProvider : CodeFixProvider
    {
        private const string title = "Add to whitelist";

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create<string>(MethodCallAnalyzer.ExternalMethodCallRule.Id, MethodCallAnalyzer.UnsafePropertyAccessRule.Id);
        
        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            foreach (var diagnostic in context.Diagnostics)
            {
                var diagnosticSpan = diagnostic.Location.SourceSpan;
                var syntaxNodes = root
                                  .FindToken(diagnosticSpan.Start)
                                  .Parent
                                  .AncestorsAndSelf();

                if (diagnostic.Id == MethodCallAnalyzer.ExternalMethodCallRule.Id)
                {
                    var invocationExpressionDecl = syntaxNodes.OfType<InvocationExpressionSyntax>().FirstOrDefault();
                    if (invocationExpressionDecl != null)
                    {
                        context.RegisterCodeFix(
                            CodeAction.Create(
                                title: title,
                                createChangedDocument: c => AddToWhitelistAsync(context.Document, invocationExpressionDecl, diagnosticSpan, c),
                                equivalenceKey: title),
                            diagnostic);
                    }
                }
                else if (diagnostic.Id == MethodCallAnalyzer.UnsafePropertyAccessRule.Id)
                {
                    var memberAccessDecl = syntaxNodes.OfType<MemberAccessExpressionSyntax>().FirstOrDefault();
                    if (memberAccessDecl != null)
                    {
                        context.RegisterCodeFix(
                            CodeAction.Create(
                                title: title,
                                createChangedDocument: c => AddToWhitelistAsync(context.Document, memberAccessDecl, diagnosticSpan, c),
                                equivalenceKey: title),
                            diagnostic);
                    }
                }
            }
        }

        private async Task<Document> AddToWhitelistAsync(Document document, InvocationExpressionSyntax invocationExpressionDecl, TextSpan diagnosticSpan, CancellationToken cancellationToken)
        {
            return document;
        }

        private async Task<Document> AddToWhitelistAsync(Document document, MemberAccessExpressionSyntax memberExpressionDecl, TextSpan diagnosticSpan, CancellationToken cancellationToken)
        {
            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var symbol = semanticModel.GetSymbolInfo(memberExpressionDecl).Symbol;

            string FormatWhitelistFunction()
            {
                var containingType = symbol.ContainingType;
                var nonGenericTypeName = containingType.Name;

                if (containingType.IsGenericType)
                {
                    var typeParameters = string.Join(",", containingType.TypeParameters.Select(x => x.Name));
                    return $"public void Whitelist{nonGenericTypeName}<{typeParameters}>({containingType} arg)";
                }

                return $"public void Whitelist{nonGenericTypeName}({containingType} arg)";
            }
            
            var configurationClass =
                //language=cs
                $@"public class MyConfiguration : AllocationConfiguration
                {{
                    {FormatWhitelistFunction()}
                    {{
                        MakeSafe(() => arg.{symbol.Name});
                    }}
                }}";

            var configurationClassNode = SyntaxFactory.ParseSyntaxTree(configurationClass).GetRoot()
                                                      .DescendantNodes().OfType<ClassDeclarationSyntax>()
                                                      .FirstOrDefault();

            var parentNamespace = oldRoot.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            if (parentNamespace == null)
                return document;

            var newParentNamespace = parentNamespace.AddMembers(configurationClassNode).NormalizeWhitespace();
            var newRoot = oldRoot.ReplaceNode(parentNamespace, newParentNamespace).NormalizeWhitespace();
            
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
