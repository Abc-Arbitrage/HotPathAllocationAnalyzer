using System;
using System.Linq;
using System.Threading;
using Buildalyzer;
using Buildalyzer.Workspaces;
using ClrHeapAllocationAnalyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConfigurationFileGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: ConfigureFileGenerator ConfigProject.csproj");
                return;
            }
            
            var csProjPath = args[0];
            var manager = new AnalyzerManager();
            var analyzer = manager.GetProject(csProjPath);
                   
            var workspace = new AdhocWorkspace();
            var roslynProject = analyzer.AddToWorkspace(workspace);

            var cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
                    
            foreach (var document in roslynProject.Documents)
            {
                if (!document.TryGetSyntaxTree(out var syntaxTree))
                    syntaxTree = document.GetSyntaxTreeAsync().Result;
                        
                if (!document.TryGetSemanticModel(out var semanticModel))
                    semanticModel = document.GetSemanticModelAsync().Result;
                            
                if ( syntaxTree != null && semanticModel != null)
                    InitializeConfiguration(syntaxTree.GetRoot(token), semanticModel, token);
            }
        }
        
        
        private static void InitializeConfiguration(SyntaxNode syntaxNode, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            Configure(syntaxNode, semanticModel, cancellationToken);

            foreach (var childNode in syntaxNode.ChildNodes())
            {
                InitializeConfiguration(childNode, semanticModel, cancellationToken);
            }
        }

        private static void Configure(SyntaxNode node, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            if (!(node is ClassDeclarationSyntax classNode))
                return;
    
            var baseTypes = classNode.BaseList?.Types.Select(b => semanticModel.GetTypeInfo(b.Type).Type);
            if (!baseTypes?.Any(b => b.Name == nameof(AllocationConfiguration) && b.ContainingNamespace.Name == typeof(AllocationConfiguration).Namespace) ?? true)
                return;
            
            foreach (var member in classNode.Members.OfType<MethodDeclarationSyntax>())
            {
                ReadConfiguration(member, semanticModel, cancellationToken);
            }
        }

        private static void ReadConfiguration(BaseMethodDeclarationSyntax member, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            var body = member.Body;
            var statements = body.Statements;

            foreach (var invocationExpression in statements.OfType<ExpressionStatementSyntax>().Select(x => x.Expression).OfType<InvocationExpressionSyntax>())
            {
                var symbol = semanticModel.GetSymbolInfo(invocationExpression, cancellationToken).Symbol;
                if (symbol.Name != nameof(AllocationConfiguration.MakeSafe))
                    continue;

                if (invocationExpression.ArgumentList.Arguments.Count != 1)
                    continue;
                
                var lambda = invocationExpression.ArgumentList.Arguments.Single().Expression as ParenthesizedLambdaExpressionSyntax;
                if (lambda == null)
                    continue;

                var method = lambda.Body as InvocationExpressionSyntax;
                var methodSymbol = semanticModel.GetSymbolInfo(method, cancellationToken).Symbol;
                
                Console.WriteLine(MethodSymbolSerializer.Serialize(methodSymbol as IMethodSymbol));
            }
        }
        
    }
}
