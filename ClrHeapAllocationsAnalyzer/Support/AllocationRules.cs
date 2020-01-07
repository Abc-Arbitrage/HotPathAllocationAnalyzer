using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;

namespace ClrHeapAllocationAnalyzer
{
    public static class AllocationRules
    {
        public const string ConfigurationDirectoryName = "ClrHeapAllocationsAnalyzer";
        public static bool IsRestrictedAllocationAttribute(AttributeData attribute)
        {
            return attribute.AttributeClass.Name == nameof(RestrictedAllocation)
                && attribute.AttributeClass.ContainingNamespace.Name == typeof(RestrictedAllocation).Namespace;
        }
    }
}
