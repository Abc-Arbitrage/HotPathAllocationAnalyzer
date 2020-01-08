using ClrHeapAllocationAnalyzer.Support;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ClrHeapAllocationAnalyzer.Analyzers
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

        public virtual void AddToWhiteList(string method)
        {
        }

        private void Analyze(SyntaxNodeAnalysisContext context)
        {
            InitializeConfiguration(context);
            
            var analyze = _forceEnableAnalysis || RestrictedAllocationAttributeHelper.HasRestrictedAllocationAttribute(context.ContainingSymbol);
            if (analyze)
                AnalyzeNode(context);
        }

        private void InitializeConfiguration(SyntaxNodeAnalysisContext context)
        {
            if (_isInitialized)
                return;

            ConfigurationHelper.ReadConfiguration(context.Node.GetLocation().SourceTree.FilePath, AddToWhiteList);

            _isInitialized = true;
        }
    }
}
