using System;
using System.IO;
using System.Linq;
using HotPathAllocationAnalyzer.Helpers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HotPathAllocationAnalyzer.Analyzers
{
    public abstract class AllocationAnalyzer : DiagnosticAnalyzer
    {
        private readonly bool _forceEnableAnalysis;
        protected bool _whitelistFound = false;


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
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterSyntaxNodeAction(Analyze, Expressions);
        }

        protected string[] GetWhiteListedSymbols(CompilationStartAnalysisContext context)
        {
            var additionalFiles = context.Options.AdditionalFiles;
            var whitelistFile = additionalFiles.FirstOrDefault(x => string.Equals(Path.GetFileName(x.Path), "whitelist.txt", StringComparison.OrdinalIgnoreCase));
            if (whitelistFile == null)
                return Array.Empty<string>();
            _whitelistFound = true;
            return whitelistFile.GetText(context.CancellationToken)
                                ?.Lines.Select(x => x.ToString())
                                .ToArray()
                   ?? Array.Empty<string>();
        }

        private void Analyze(SyntaxNodeAnalysisContext context)
        {
            var analyze = _forceEnableAnalysis
                          || (AttributeHelper.ShouldAnalyzeNode(context.ContainingSymbol)
                              && !AttributeHelper.HasIgnoreAllocationAttribute(context.ContainingSymbol));
            if (analyze)
                AnalyzeNode(context);
        }
    }
}
