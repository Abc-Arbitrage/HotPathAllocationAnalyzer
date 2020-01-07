using System.IO;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ClrHeapAllocationAnalyzer
{
    public abstract class AllocationAnalyzer : DiagnosticAnalyzer
    {
        private readonly bool _forceEnableAnalysis;
        private bool _isInitialized;

        protected abstract SyntaxKind[] Expressions { get; }

        protected abstract void AnalyzeNode(SyntaxNodeAnalysisContext context);

        public AllocationAnalyzer()
        {
        }

        public AllocationAnalyzer(bool forceEnableAnalysis)
        {
            _forceEnableAnalysis = forceEnableAnalysis;
        }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(Analyze, Expressions);
        }

        private void InitializeConfiguration(SyntaxNodeAnalysisContext context)
        {
            if (_isInitialized)
                return;

            var filePath = context.Node.GetLocation().SourceTree.FilePath;

            if (!string.IsNullOrEmpty(filePath))
            {
                var configDir = FindConfigurationDirectory(filePath);
                if (!string.IsNullOrEmpty(configDir))
                {
                    var whitelist = File.ReadAllLines(Path.Combine(configDir, AllocationRules.WhitelistFileName));

                    foreach (var item in whitelist)
                    {
                        AddToWhiteList(item);
                    }
                }
            }
            
            _isInitialized = true;
        }

        private static string FindConfigurationDirectory(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;
            
            var directory = Directory.Exists(filePath) ? filePath : Path.GetDirectoryName(filePath);

            if (Directory.Exists(Path.Combine(directory, AllocationRules.ConfigurationDirectoryName)))
            {
                var referencesFile = Directory.EnumerateFiles(Path.Combine(directory, AllocationRules.ConfigurationDirectoryName)).FirstOrDefault(x => x.EndsWith(AllocationRules.WhitelistFileName));
                if (referencesFile != null)
                    return Path.Combine(directory, AllocationRules.ConfigurationDirectoryName);
            }

            return FindConfigurationDirectory(Directory.GetParent(directory)?.FullName);
        }
        
        public virtual void AddToWhiteList(string method)
        {
        }

        private void Analyze(SyntaxNodeAnalysisContext context)
        {
            InitializeConfiguration(context);
            
            var analyze = _forceEnableAnalysis || HasRestrictedAllocationAttribute(context.ContainingSymbol);
            if (analyze)
                AnalyzeNode(context);
        }

        public static bool HasRestrictedAllocationAttribute(ISymbol containingSymbol)
        {
            if (containingSymbol.GetAttributes().Any(AllocationRules.IsRestrictedAllocationAttribute))
                return true;

            if (containingSymbol is IMethodSymbol method)
            {
                if (method.ExplicitInterfaceImplementations.Any(x => x.GetAttributes().Any(AllocationRules.IsRestrictedAllocationAttribute)))
                    return true;
                if (ImplementedInterfaceHasAttribute(method))
                    return true;
                if (method.IsOverride && method.OverriddenMethod.GetAttributes().Any(AllocationRules.IsRestrictedAllocationAttribute))
                    return true;
                if (method.IsOverride)
                    return HasRestrictedAllocationAttribute(method.OverriddenMethod);
            }    
            
            if (containingSymbol is IPropertySymbol property)
            {
                return HasRestrictedAllocationAttribute(property.GetMethod);
            }

            return false;
        }

        private static bool ImplementedInterfaceHasAttribute(ISymbol method)
        {
            var type = method.ContainingType;
            
            foreach (var iface in type.AllInterfaces)
            {
                var interfaceMethods = iface.GetMembers().OfType<IMethodSymbol>();
                var interfaceMethod = interfaceMethods.SingleOrDefault(x => type.FindImplementationForInterfaceMember(x).Equals(method));
                if (interfaceMethod?.GetAttributes().Any(AllocationRules.IsRestrictedAllocationAttribute)?? false)
                    return true;
            }

            return false;
        }
        
    }
}
