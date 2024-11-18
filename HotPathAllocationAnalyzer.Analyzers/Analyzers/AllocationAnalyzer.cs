using HotPathAllocationAnalyzer.Helpers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HotPathAllocationAnalyzer.Analyzers;

public abstract class AllocationAnalyzer : DiagnosticAnalyzer
{
    private readonly bool _forceEnableAnalysis;

    protected abstract SyntaxKind[] Expressions { get; }

    protected AllocationAnalyzer()
    {
    }

    protected AllocationAnalyzer(bool forceEnableAnalysis)
    {
        _forceEnableAnalysis = forceEnableAnalysis;
    }

    protected bool ShouldAnalyzeNode(SyntaxNodeAnalysisContext context)
        => _forceEnableAnalysis
           || (context.ContainingSymbol is not null
               && AttributeHelper.ShouldAnalyzeNode(context.ContainingSymbol)
               && !AttributeHelper.HasIgnoreAllocationAttribute(context.ContainingSymbol));
}

public abstract class SyntaxNodeAllocationAnalyzer : AllocationAnalyzer
{
    protected SyntaxNodeAllocationAnalyzer()
    {
    }

    protected SyntaxNodeAllocationAnalyzer(bool forceEnableAnalysis)
        : base(forceEnableAnalysis)
    {
    }

    protected abstract void AnalyzeNode(SyntaxNodeAnalysisContext context);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterSyntaxNodeAction(
            syntaxNodeContext =>
            {
                if (ShouldAnalyzeNode(syntaxNodeContext))
                    AnalyzeNode(syntaxNodeContext);
            },
            Expressions
        );
    }
}
