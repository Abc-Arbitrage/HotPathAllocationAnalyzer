using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buildalyzer;
using Buildalyzer.Workspaces;
using ClrHeapAllocationAnalyzer.Helpers;
using ClrHeapAllocationAnalyzer.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Project = Microsoft.CodeAnalysis.Project;

namespace ClrHeapAllocationAnalyzer.Configuration
{
    public class ConfigurationReader
    {
        private readonly Project _configurationProject; 
        
        public ConfigurationReader(string configurationProjectDirectory)
        {
            if (!Directory.Exists(configurationProjectDirectory))
                throw new ArgumentException($"Directory {configurationProjectDirectory} does not exist");

            var csProjPath = Directory.EnumerateFiles(configurationProjectDirectory, "*.csproj").SingleOrDefault();
            if (string.IsNullOrEmpty(csProjPath)) 
                throw new ArgumentException($"Could not find csproj file in {configurationProjectDirectory} directory");
            
            var manager = new AnalyzerManager();
            var analyzer = manager.GetProject(csProjPath);
            analyzer.SetGlobalProperty("IsRunningClrHeapAllocationAnalyzerConfiguration", "true");
            
            var workspace = new AdhocWorkspace();
            _configurationProject = analyzer.AddToWorkspace(workspace);
        }

        public async Task<IEnumerable<string>> GenerateWhitelistAsync(CancellationToken token)
        {
            var whiteListSymbols = new HashSet<string>();
            
            foreach (var document in _configurationProject.Documents)
            {
                var syntaxTree = await document.GetSyntaxTreeAsync();
                var semanticModel = await document.GetSemanticModelAsync();

                if (syntaxTree != null && semanticModel != null)
                {
                    var configurationClasses = GetConfigurationClasses(syntaxTree.GetRoot(token), semanticModel);
                    whiteListSymbols.UnionWith(configurationClasses.SelectMany(x => GenerateWhitelistSymbols(x, semanticModel, token)));
                }
            }

            return whiteListSymbols;
        }

        private static IEnumerable<string> GenerateWhitelistSymbols(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, CancellationToken token)
            => classDecl.Members.OfType<MethodDeclarationSyntax>().SelectMany(m => GenerateWhitelistSymbol(m, semanticModel, token));

        private static IEnumerable<string> GenerateWhitelistSymbol(BaseMethodDeclarationSyntax methodDecl, SemanticModel semanticModel, CancellationToken token)
        {
            var body = methodDecl.Body;
            var statements = body.Statements;

            var invocationsExpr = statements.OfType<ExpressionStatementSyntax>()
                                           .Select(x => x.Expression)
                                           .OfType<InvocationExpressionSyntax>();

            foreach (var invocationExpr in invocationsExpr)
            {
                var symbol = semanticModel.GetSymbolInfo(invocationExpr, token).Symbol;
                if (symbol?.Name != nameof(AllocationConfiguration.MakeSafe))
                    continue;

                var arguments = invocationExpr.ArgumentList.Arguments;
                
                if (arguments.Count != 1)
                    continue;

                var lambdaExpr = arguments[0].Expression as ParenthesizedLambdaExpressionSyntax;
                if (lambdaExpr == null)
                    continue;

                var childSymbol = semanticModel.GetSymbolInfo(lambdaExpr.Body, token).Symbol;
                switch (childSymbol)
                {
                    case IMethodSymbol methodExpr:
                    {
                        yield return MethodSymbolSerializer.Serialize(methodExpr);
                        break;
                    }
                    case IPropertySymbol memberExpr:
                    {
                        yield return MethodSymbolSerializer.Serialize(memberExpr);
                        break;
                    }
                }
            }
        }

        private static IEnumerable<ClassDeclarationSyntax> GetConfigurationClasses(SyntaxNode syntaxNode, SemanticModel model)
        {
            var configurationClasses = new List<ClassDeclarationSyntax>();

            var classNode = AllocationRules.GetConfigurationClass(syntaxNode, model);
            if (classNode != null)
                configurationClasses.Add(classNode);
            
            foreach (var child in syntaxNode.ChildNodes())
                configurationClasses.AddRange(GetConfigurationClasses(child, model));

            return configurationClasses;
        }
    }
}
