using Microsoft.CodeAnalysis;

namespace HotPathAllocationAnalyzer.Helpers
{
    public static class MethodSymbolSerializer
    {
        public static string Serialize(IMethodSymbol symbol)
        {
            return symbol.OriginalDefinition.ToString();
        }
        
        public static string Serialize(IPropertySymbol symbol)
        {
            return symbol.OriginalDefinition.ToString();
        }
    }
}
