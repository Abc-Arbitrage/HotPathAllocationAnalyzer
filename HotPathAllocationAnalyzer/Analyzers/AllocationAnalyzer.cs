using System;
using HotPathAllocationAnalyzer.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HotPathAllocationAnalyzer.Analyzers
{
    public abstract class AllocationAnalyzer : DiagnosticAnalyzer
    {
        private readonly bool _forceEnableAnalysis;

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
