using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;

namespace ClrHeapAllocationAnalyzer
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
            context.RegisterSyntaxNodeAction(Analyze, Expressions);
        }

        private void Analyze(SyntaxNodeAnalysisContext context)
        {
            if (!_forceEnableAnalysis && !context.ContainingSymbol.GetAttributes().Any(AllocationRules.IsRestrictedAllocationAttribute))
            {
                return;
            }

            AnalyzeNode(context);
        }
    }
}
