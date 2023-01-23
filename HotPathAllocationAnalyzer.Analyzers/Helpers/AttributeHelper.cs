using System;
using System.Linq;
using HotPathAllocationAnalyzer.Support;
using Microsoft.CodeAnalysis;

namespace HotPathAllocationAnalyzer.Helpers
{
    internal static class AttributeHelper
    {
        public static bool ShouldAnalyzeNode(ISymbol containingSymbol)
        {
            if (containingSymbol.Kind == SymbolKind.Property)
                return false;
            if (containingSymbol is not IMethodSymbol methodSymbol)
                return HasNoAllocationAttribute(containingSymbol);
            if (methodSymbol.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet)
            {
                return HasNoAllocationAttribute(methodSymbol) || (methodSymbol.AssociatedSymbol != null && HasNoAllocationAttribute(methodSymbol.AssociatedSymbol));
            }

            return HasNoAllocationAttribute(containingSymbol);
        }
        
        public static bool HasNoAllocationAttribute(ISymbol containingSymbol)
        {
            return FindAttribute(containingSymbol, AllocationRules.IsNoAllocationAttribute);
        }
        
        public static bool HasIgnoreAllocationAttribute(ISymbol containingSymbol)
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
                    if (method.IsOverride && method.OverriddenMethod != null && FindAttribute(method.OverriddenMethod, attribute))
                        return true;
                    if (ContainingTypeHasAttribute(method, attribute))
                        return true;
                }

                return false;
            }
            catch (Exception e)
            {
                throw new Exception($"Error while looking for {nameof(NoAllocation)}", e);
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

        private static bool ContainingTypeHasAttribute(ISymbol method, Func<AttributeData, bool> attribute)
        {
            var type = method.ContainingType;
            if (type.GetAttributes().Any(attribute))
                return true;
            while (type != null)
            {
                if (type.BaseType?.GetAttributes().Any(attribute) == true)
                    return true;

                type = type.BaseType;
            }

            return false;
        }
    }
}