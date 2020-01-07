using Microsoft.CodeAnalysis;
using System.Linq;

namespace ClrHeapAllocationAnalyzer
{
    public static class MethodSymbolSerializer
    {
        public static string Serialize(IMethodSymbol symbol)
        {
            return symbol.ToString();
        }
    }
}
