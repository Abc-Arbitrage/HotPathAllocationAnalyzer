using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;

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
            var  analyze = ShouldAnalyze(context.ContainingSymbol);

            if (analyze)
                AnalyzeNode(context);
        }

        private bool ShouldAnalyze(ISymbol containingSymbol)
        {
            if (_forceEnableAnalysis)
                return true;
            
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
                    return ShouldAnalyze(method.OverriddenMethod);
            }

            return false;
        }

        private bool ImplementedInterfaceHasAttribute(IMethodSymbol method)
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
        
        public static bool IsInterfaceImplementation(IMethodSymbol method)
        {
            return method.ContainingType.AllInterfaces.SelectMany(@interface => @interface.GetMembers()
                                                                                          .OfType<IMethodSymbol>())
                         .Any(interfaceMethod => method.ContainingType.FindImplementationForInterfaceMember(interfaceMethod)
                                                       .Equals(method));
        }
    }
}
