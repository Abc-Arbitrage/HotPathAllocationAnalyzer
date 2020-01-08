using System.Linq;
using Microsoft.CodeAnalysis;

namespace ClrHeapAllocationAnalyzer.Support
{
    static internal class RestrictedAllocationAttributeHelper
    {
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
            
            if (containingSymbol is IPropertySymbol property)
            {
                return HasRestrictedAllocationAttribute(property.GetMethod);
            }

            return false;
        }
    
        private static bool ImplementedInterfaceHasAttribute(ISymbol method)
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