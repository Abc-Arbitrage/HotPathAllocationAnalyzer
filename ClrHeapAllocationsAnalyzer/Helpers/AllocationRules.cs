using Microsoft.CodeAnalysis;

namespace ClrHeapAllocationAnalyzer.Support
{
    internal static class AllocationRules
    {
        public const string ConfigurationDirectoryName = "ClrHeapAllocationsAnalyzer";
        
        public const string WhitelistFileName = "whitelist.txt";
        
        public static bool IsRestrictedAllocationAttribute(AttributeData attribute)
        {
            return attribute.AttributeClass.Name == nameof(RestrictedAllocation)
                && attribute.AttributeClass.ContainingNamespace.Name == typeof(RestrictedAllocation).Namespace;
        }
    }
}
