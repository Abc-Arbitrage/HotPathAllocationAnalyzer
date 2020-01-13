using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace HotPathAllocationAnalyzer.Helpers
{
    internal static class RestrictedAllocationAttributeHelper
    {
        public static bool HasRestrictedAllocationAttribute(ISymbol containingSymbol)
        {
            return FindAttribute(containingSymbol, AllocationRules.IsRestrictedAllocationAttribute);
        }
        
        public static bool HasRestrictedAllocationIgnoreAttribute(ISymbol containingSymbol)
        {
            return FindAttribute(containingSymbol, AllocationRules.IsIgnoreAllocationAttribute);
        }

        private static bool FindAttribute(ISymbol containingSymbol, Func<AttributeData, bool> attribute)
        {
            try
            {
                if (containingSymbol == null)
                    return false;

                if (containingSymbol.GetAttributes().Any(attribute))
                    return true;

                if (containingSymbol is IMethodSymbol method)
                {
                    if (method.ExplicitInterfaceImplementations.Any(x => x.GetAttributes().Any(attribute)))
                        return true;
                    if (ImplementedInterfaceHasAttribute(method, attribute))
                        return true;
                    if (method.IsOverride && method.OverriddenMethod != null && method.OverriddenMethod.GetAttributes().Any(attribute))
                        return true;
                    if (method.IsOverride && method.OverriddenMethod != null)
                        return FindAttribute(method.OverriddenMethod, attribute);
                }

                return false;
            }
            catch (Exception e)
            {
                throw new Exception("Error while looking for RestrictedAttribute", e);
            }
        }

        private static bool ImplementedInterfaceHasAttribute(ISymbol method, Func<AttributeData, bool> attribute)
        {
            var type = method.ContainingType;
            
            foreach (var iface in type.AllInterfaces)
            {
                var interfaceMethods = iface.GetMembers().OfType<IMethodSymbol>();
                var interfaceMethod = interfaceMethods.SingleOrDefault(x => type.FindImplementationForInterfaceMember(x)?.Equals(method) ?? false);
                if (interfaceMethod?.GetAttributes().Any(attribute)?? false)
                    return true;
            }

            return false;
        }
    }
}