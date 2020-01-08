using System.Linq;
using ClrHeapAllocationAnalyzer.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClrHeapAllocationAnalyzer.Helpers
{
    public static class AllocationRules
    {
        public const string ConfigurationDirectoryName = "ClrHeapAllocationsAnalyzer";
        
        public const string WhitelistFileName = "whitelist.txt";
        
        public static bool IsRestrictedAllocationAttribute(AttributeData attribute)
        {
            return attribute.AttributeClass.Name == nameof(RestrictedAllocation)
                && attribute.AttributeClass.ContainingNamespace.ToDisplayString() == typeof(RestrictedAllocation).Namespace;
        }

        public static ClassDeclarationSyntax GetConfigurationClass(SyntaxNode syntaxNode, SemanticModel semanticModel)
        {
            bool IsConfigurationBaseType(ITypeSymbol typeSymbol)
            {
                return typeSymbol.Name == nameof(AllocationConfiguration)
                       && typeSymbol.ContainingNamespace.ToString() == typeof(AllocationConfiguration).Namespace;
                
            }
            
            if (syntaxNode is ClassDeclarationSyntax classDeclarationSyntax)
            {
                var baseTypes = classDeclarationSyntax.BaseList?.Types
                                                      .Select(t => semanticModel.GetTypeInfo(t.Type).Type);
                if (baseTypes?.Any(IsConfigurationBaseType) ?? false)
                    return classDeclarationSyntax;
            }

            return null;
        }
    }
}
