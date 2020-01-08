using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ClrHeapAllocationAnalyzer.Helpers
{
    internal static class RestrictedAllocationAttributeHelper
    {
        public static bool HasRestrictedAllocationAttribute(ISymbol containingSymbol)
        {
            try
            {
                if (containingSymbol == null)
                    return false;

                if (containingSymbol.GetAttributes().Any(AllocationRules.IsRestrictedAllocationAttribute))
                    return true;

                if (containingSymbol is IMethodSymbol method)
                {
                    if (method.ExplicitInterfaceImplementations.Any(x => x.GetAttributes().Any(AllocationRules.IsRestrictedAllocationAttribute)))
                        return true;
                    if (ImplementedInterfaceHasAttribute(method))
                        return true;
                    if (method.IsOverride && method.OverriddenMethod != null && method.OverriddenMethod.GetAttributes().Any(AllocationRules.IsRestrictedAllocationAttribute))
                        return true;
                    if (method.IsOverride && method.OverriddenMethod != null)
                        return HasRestrictedAllocationAttribute(method.OverriddenMethod);
                }

                if (containingSymbol is IPropertySymbol property && property.GetMethod != null)
                {
                    return HasRestrictedAllocationAttribute(property.GetMethod);
                }

                return false;
            }
            catch (Exception e)
            {
                throw new Exception("Error while looking for RestrictedAttribute", e);
            }
        }
    
        private static bool ImplementedInterfaceHasAttribute(ISymbol method)
        {
            var type = method.ContainingType;
            
            foreach (var iface in type.AllInterfaces)
            {
                var interfaceMethods = iface.GetMembers().OfType<IMethodSymbol>();
                var interfaceMethod = interfaceMethods.SingleOrDefault(x => type.FindImplementationForInterfaceMember(x)?.Equals(method) ?? false);
                if (interfaceMethod?.GetAttributes().Any(AllocationRules.IsRestrictedAllocationAttribute)?? false)
                    return true;
            }

            return false;
        }
    }
}