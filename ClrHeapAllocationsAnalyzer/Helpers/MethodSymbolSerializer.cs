using Microsoft.CodeAnalysis;

namespace ClrHeapAllocationAnalyzer.Helpers
{
    public static class MethodSymbolSerializer
    {
        public static string Serialize(IMethodSymbol symbol)
        {
            return symbol.ToString();
        }
        
        public static string Serialize(IPropertySymbol symbol)
        {
            return symbol.ToString();
        }
    }
}