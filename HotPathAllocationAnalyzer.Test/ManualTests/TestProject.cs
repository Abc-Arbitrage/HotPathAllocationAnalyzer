using System;
using System.Collections.Immutable;
using System.Linq;
using Buildalyzer;
using Buildalyzer.Workspaces;
using HotPathAllocationAnalyzer.Analyzers;
using HotPathAllocationAnalyzer.Test.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HotPathAllocationAnalyzer.Test.ManualTests
{
    [TestClass]
    public class TestProject : AllocationAnalyzerTests
    {
        [TestMethod, Ignore]
        public void AnalyzeProgram()
        {
            if (!System.Diagnostics.Debugger.IsAttached) 
                return;
            
            var csProjPath = @"C:\Dev\dotnet\src\Abc.Trading.Strategies\Abc.Trading.Services.Common\Abc.Trading.Services.Common.csproj";
            
            var testAnalyser = new MethodCallAnalyzer();
            

            var manager = new AnalyzerManager();

            var analyzer = manager.GetProject(csProjPath);
            analyzer.SetGlobalProperty("IsRunningHotPathAllocationAnalyzerConfiguration", "true");
            analyzer.IgnoreFaultyImports = true;

            var workspace = new AdhocWorkspace();
            var project = analyzer.AddToWorkspace(workspace)
                                  .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, metadataImportOptions: MetadataImportOptions.Public ))
                                  .WithAnalyzerReferences(new AnalyzerReference[0]);

            var trees = project.Documents.Select(x => x.GetSyntaxTreeAsync().Result)
                               .ToArray();

            // Run the code tree through the analyzer and record the allocations it reports
            var compilation = CSharpCompilation.Create(project.AssemblyName, trees, project.MetadataReferences, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, metadataImportOptions: MetadataImportOptions.Public));
     
            var diagnostics = compilation.GetDiagnostics();
            if (diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error) > 0)
            {
                var msg = "There were Errors in the sample code\n";
                Console.WriteLine(msg);
                foreach (var info in diagnostics)
                    Console.WriteLine(info);
            }
            
            var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create((DiagnosticAnalyzer)testAnalyser));
            var allocations = compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult().Distinct(DiagnosticEqualityComparer.Instance).ToList();

        }
    }
}
