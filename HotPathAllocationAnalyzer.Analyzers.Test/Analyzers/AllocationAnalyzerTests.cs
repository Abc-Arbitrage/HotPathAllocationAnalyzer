﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HotPathAllocationAnalyzer.Test.Analyzers
{
    public class TestAdditionalFile : AdditionalText
    {
        public TestAdditionalFile(string path, string content)
        {
            Path = path;
            Content = content;
        }

        public string Content { get; set; }

        public override SourceText GetText(CancellationToken cancellationToken = new CancellationToken())
        => SourceText.From(Content);

        public override string Path { get; }
    }

    public abstract class AllocationAnalyzerTests
    {
        private static readonly List<MetadataReference> references = (from item in AppDomain.CurrentDomain.GetAssemblies() 
                                                                      where !item.IsDynamic && item.Location != null && item.Location != string.Empty 
                                                                      select MetadataReference.CreateFromFile(item.Location))
                                                                     .Cast<MetadataReference>().ToList();

        protected IList<SyntaxNode> GetExpectedDescendants(IEnumerable<SyntaxNode> nodes, ImmutableArray<SyntaxKind> expected)
        {
            var descendants = new List<SyntaxNode>();
            foreach (var node in nodes)
            {
                if (expected.Any(e => e == node.Kind()))
                {
                    descendants.Add(node);
                    continue;
                }

                foreach (var child in node.ChildNodes())
                {
                    if (expected.Any(e => e == child.Kind()))
                    {
                        descendants.Add(child);
                        continue;
                    }

                    if (child.ChildNodes().Count() > 0)
                        descendants.AddRange(GetExpectedDescendants(child.ChildNodes(), expected));
                }
            }
            return descendants;
        }

        protected Info ProcessCode(DiagnosticAnalyzer analyzer, string sampleProgram,
            ImmutableArray<SyntaxKind> expected, bool allowBuildErrors = false, TestAdditionalFile[] additionalFiles = null)
        {
            var options = new CSharpParseOptions(kind: SourceCodeKind.Script, languageVersion: LanguageVersion.CSharp10);
            var tree = CSharpSyntaxTree.ParseText(sampleProgram, options);
            var compilation = CSharpCompilation.Create("Test", new[] { tree }, references);

            var diagnostics = compilation.GetDiagnostics();
            if (diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error) > 0)
            {
                var msg = "There were Errors in the sample code\n";
                if (allowBuildErrors == false)
                    Assert.Fail(msg + string.Join("\n", diagnostics));
                else
                    Console.WriteLine(msg + string.Join("\n", diagnostics));
            }

            var semanticModel = compilation.GetSemanticModel(tree);
            var matches = GetExpectedDescendants(tree.GetRoot().ChildNodes(), expected);

            // Run the code tree through the analyzer and record the allocations it reports
            var analyzerAdditionalFiles = (additionalFiles ?? Array.Empty<TestAdditionalFile>()).Cast<AdditionalText>().ToImmutableArray();
            var analyzerOptions = new AnalyzerOptions(analyzerAdditionalFiles);
            var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create(analyzer), analyzerOptions);
            var allocations = compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult().Distinct(DiagnosticEqualityComparer.Instance).ToList();

            return new Info
            {
                Options = options,
                Tree = tree,
                Compilation = compilation,
                Diagnostics = diagnostics,
                SemanticModel = semanticModel,
                Matches = matches,
                Allocations = allocations,
            };
        }

        protected class Info
        {
            public CSharpParseOptions Options { get; set; }
            public SyntaxTree Tree { get; set; }
            public CSharpCompilation Compilation { get; set; }
            public ImmutableArray<Diagnostic> Diagnostics { get; set; }
            public SemanticModel SemanticModel { get; set; }
            public IList<SyntaxNode> Matches { get; set; }
            public List<Diagnostic> Allocations { get; set; }
        }
    }
}