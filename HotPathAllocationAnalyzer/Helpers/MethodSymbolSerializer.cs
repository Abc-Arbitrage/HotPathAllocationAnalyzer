using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace HotPathAllocationAnalyzer.Helpers
{
    public static class MethodSymbolSerializer
    {
        private static readonly SymbolDisplayFormat? _format = SymbolDisplayFormat.CSharpErrorMessageFormat.RemoveMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        public static string Serialize(IMethodSymbol symbol)
        {
            return SymbolDisplay.ToDisplayString(symbol.OriginalDefinition, _format);
        }
        
        public static string Serialize(IPropertySymbol symbol)
        {
            return SymbolDisplay.ToDisplayString(symbol.OriginalDefinition, _format);
        }
    }
}
