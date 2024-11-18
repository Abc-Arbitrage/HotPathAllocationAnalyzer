using System.Linq;
using System.Runtime.CompilerServices;
using HotPathAllocationAnalyzer.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HotPathAllocationAnalyzer.Helpers
{
    public static class AllocationRules
    {
        public const string ConfigurationRootDirectoryName = "Analyzers";
        public const string ConfigurationDirectoryName = "HotPathAllocationAnalyzer.Analyzers";
        
        public const string WhitelistFileName = "whitelist.txt";
        
        public static bool IsNoAllocationAttribute(AttributeData attribute)
        {
            return attribute.AttributeClass?.Name == nameof(NoAllocation)
                && attribute.AttributeClass.ContainingNamespace.ToDisplayString() == typeof(NoAllocation).Namespace;
        }

        public static bool IsIgnoreAllocationAttribute(AttributeData attribute)
        {
            return attribute.AttributeClass?.Name == nameof(IgnoreAllocation)
                && attribute.AttributeClass.ContainingNamespace.ToDisplayString() == typeof(IgnoreAllocation).Namespace;
        }

        public static bool IsCompilerGeneratedAttribute(AttributeData attribute)
        {
            return attribute.AttributeClass?.Name == nameof(CompilerGeneratedAttribute)
                   && attribute.AttributeClass.ContainingNamespace.ToDisplayString() == typeof(CompilerGeneratedAttribute).Namespace;
        }

        public static bool IsMakeSafeAttribute(AttributeData attribute)
        {
            return attribute.AttributeClass?.Name == nameof(MakeSafe)
                   && attribute.AttributeClass.ContainingNamespace.ToDisplayString() == typeof(MakeSafe).Namespace;
        }

        public static ClassDeclarationSyntax? GetConfigurationClass(SyntaxNode syntaxNode, SemanticModel semanticModel)
        {
            bool IsConfigurationBaseType(ITypeSymbol? typeSymbol)
            {
                return typeSymbol?.Name == nameof(AllocationConfiguration)
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
