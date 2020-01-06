using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;

namespace ClrHeapAllocationAnalyzer
{
    public class AllocationRules
    {
        public static bool IsRestrictedAllocationAttribute(AttributeData attribute)
        {
            return attribute.AttributeClass.Name == nameof(RestrictedAllocation)
                && attribute.AttributeClass.ContainingNamespace.Name == typeof(RestrictedAllocation).Namespace;
        }
    }
}
