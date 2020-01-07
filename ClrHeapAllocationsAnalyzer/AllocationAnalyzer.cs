using System.IO;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;
using System.Threading;
using Buildalyzer;
using Buildalyzer.Workspaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClrHeapAllocationAnalyzer
{
    public abstract class AllocationAnalyzer : DiagnosticAnalyzer
    {
        private readonly bool _forceEnableAnalysis;
        private bool _isInitialized;

        protected abstract SyntaxKind[] Expressions { get; }

        protected abstract void AnalyzeNode(SyntaxNodeAnalysisContext context);

        public AllocationAnalyzer()
        {
        }

        public AllocationAnalyzer(bool forceEnableAnalysis)
        {
            _forceEnableAnalysis = forceEnableAnalysis;
        }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(Analyze, Expressions);
        }

        private void InitializeConfiguration(SyntaxNodeAnalysisContext context)
        {
            if (_isInitialized)
                return;

            var filePath = context.Node.GetLocation().SourceTree.FilePath;

            if (string.IsNullOrEmpty(filePath))
            {
                InitializeConfiguration(context.Node.SyntaxTree.GetRoot(), context.SemanticModel, context.CancellationToken);
            }
            else
            {
                // TODO
                var configFile = FindConfigurationFile(filePath);
                if (!string.IsNullOrEmpty(configFile))
                {
                    var manager = new AnalyzerManager();
                    var analyzer = manager.GetProject(configFile);
                    // var results = analyzer.Build();
                   
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
            }
            
            _isInitialized = true;
        }

        private string FindConfigurationFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;
            
            var directory = Directory.Exists(filePath) ? filePath : Path.GetDirectoryName(filePath);

            if (Directory.Exists(Path.Combine(directory, AllocationRules.ConfigurationDirectoryName)))
            {
                var projectFile = Directory.EnumerateFiles(Path.Combine(directory, AllocationRules.ConfigurationDirectoryName), "*.csproj").FirstOrDefault();
                if (projectFile != null)
                    return projectFile;
            }

            return FindConfigurationFile(Directory.GetParent(directory)?.FullName);
        }

        private void InitializeConfiguration(SyntaxNode syntaxNode, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            Configure(syntaxNode, semanticModel, cancellationToken);

            foreach (var childNode in syntaxNode.ChildNodes())
            {
                InitializeConfiguration(childNode, semanticModel, cancellationToken);
            }
        }

        private void Configure(SyntaxNode node, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            if (!(node is ClassDeclarationSyntax classNode))
                return;
    
            var baseTypes = classNode.BaseList?.Types.Select(b => semanticModel.GetTypeInfo(b.Type).Type);
            if (!baseTypes?.Any(b => b.Name == nameof(IAllocationConfiguration) && b.ContainingNamespace.Name == typeof(IAllocationConfiguration).Namespace) ?? true)
                return;
            
            foreach (var member in classNode.Members.OfType<MethodDeclarationSyntax>())
            {
                ReadConfiguration(member, semanticModel, cancellationToken);
            }
        }

        private void ReadConfiguration(BaseMethodDeclarationSyntax member, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            var body = member.Body;
            var statements = body.Statements;

            foreach (var invocationExpression in statements.OfType<ExpressionStatementSyntax>().Select(x => x.Expression).OfType<InvocationExpressionSyntax>())
            {
                var symbol = semanticModel.GetSymbolInfo(invocationExpression, cancellationToken).Symbol;
                if (symbol.Name != "MakeSafe")
                    continue;

                if (invocationExpression.ArgumentList.Arguments.Count != 1)
                    continue;
                
                var lambda = invocationExpression.ArgumentList.Arguments.Single().Expression as ParenthesizedLambdaExpressionSyntax;
                if (lambda == null)
                    continue;

                var method = lambda.Body as InvocationExpressionSyntax;
                var methodSymbol = semanticModel.GetSymbolInfo(method, cancellationToken).Symbol;
                AddToWhiteList(methodSymbol);
            }
        }

        protected virtual void AddToWhiteList(ISymbol methodSymbol)
        {
        }

        private void Analyze(SyntaxNodeAnalysisContext context)
        {
            InitializeConfiguration(context);
            
            var analyze = _forceEnableAnalysis || HasRestrictedAllocationAttribute(context.ContainingSymbol);
            if (analyze)
                AnalyzeNode(context);
        }

        public static bool HasRestrictedAllocationAttribute(ISymbol containingSymbol)
        {
            if (containingSymbol.GetAttributes().Any(AllocationRules.IsRestrictedAllocationAttribute))
                return true;

            if (containingSymbol is IMethodSymbol method)
            {
                if (method.ExplicitInterfaceImplementations.Any(x => x.GetAttributes().Any(AllocationRules.IsRestrictedAllocationAttribute)))
                    return true;
                if (ImplementedInterfaceHasAttribute(method))
                    return true;
                if (method.IsOverride && method.OverriddenMethod.GetAttributes().Any(AllocationRules.IsRestrictedAllocationAttribute))
                    return true;
                if (method.IsOverride)
                    return HasRestrictedAllocationAttribute(method.OverriddenMethod);
            }

            return false;
        }

        private static bool ImplementedInterfaceHasAttribute(IMethodSymbol method)
        {
            var type = method.ContainingType;
            
            foreach (var iface in type.AllInterfaces)
            {
                var interfaceMethods = iface.GetMembers().OfType<IMethodSymbol>();
                var interfaceMethod = interfaceMethods.SingleOrDefault(x => type.FindImplementationForInterfaceMember(x).Equals(method));
                if (interfaceMethod?.GetAttributes().Any(AllocationRules.IsRestrictedAllocationAttribute)?? false)
                    return true;

            }

            return false;
        }
        
    }
}
