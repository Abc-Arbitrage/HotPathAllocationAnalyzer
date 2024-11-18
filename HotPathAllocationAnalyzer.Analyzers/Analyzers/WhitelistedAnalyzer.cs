using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using HotPathAllocationAnalyzer.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HotPathAllocationAnalyzer.Analyzers;

public abstract class WhitelistedAnalyzer : AllocationAnalyzer
{
    private readonly HashSet<string> _alwaysWhitelistedSymbols = new(); // For tests

    protected WhitelistedAnalyzer()
    {
    }

    protected WhitelistedAnalyzer(bool forceEnableAnalysis)
        : base(forceEnableAnalysis)
    {
    }

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(startContext =>
        {
            var whitelist = LoadWhitelistedSymbols(startContext.Options.AdditionalFiles, startContext.CancellationToken);

            // For tests
            if (_alwaysWhitelistedSymbols.Count != 0)
                (whitelist ??= []).UnionWith(_alwaysWhitelistedSymbols);

            var analysisContext = new WhitelistedAnalysisContext(whitelist);

            startContext.RegisterSyntaxNodeAction(
                syntaxNodeContext =>
                {
                    if (ShouldAnalyzeNode(syntaxNodeContext))
                        AnalyzeNode(analysisContext, syntaxNodeContext);
                },
                Expressions
            );
        });
    }

    protected abstract void AnalyzeNode(WhitelistedAnalysisContext analysisContext, SyntaxNodeAnalysisContext syntaxNodeContext);

    private static HashSet<string>? LoadWhitelistedSymbols(ImmutableArray<AdditionalText> additionalFiles, CancellationToken cancellationToken)
    {
        var whitelist = default(HashSet<string>);

        foreach (var additionalFile in additionalFiles)
        {
            if (!string.Equals(Path.GetFileName(additionalFile.Path), "whitelist.txt", StringComparison.OrdinalIgnoreCase))
                continue;

            whitelist ??= [];
            whitelist.UnionWith(
                additionalFile.GetText(cancellationToken)?
                              .Lines.Select(x => x.ToString().Trim())
                              .Where(x => !string.IsNullOrEmpty(x) && !x.StartsWith("#"))
                ?? []
            );
        }

        return whitelist;
    }

    /// <summary>
    /// For tests
    /// </summary>
    public void AddToWhiteList(string method)
        => _alwaysWhitelistedSymbols.Add(method);

    protected class WhitelistedAnalysisContext
    {
        private readonly HashSet<string>? _whitelist;

        public bool HasWhitelist => _whitelist is null;
        public bool IsEmptyWhitelist => _whitelist is null or { Count: 0 };

        public WhitelistedAnalysisContext(HashSet<string>? whitelist)
        {
            _whitelist = whitelist;
        }

        public bool IsWhitelisted(IMethodSymbol methodInfo)
            => IsWhitelisted(MethodSymbolSerializer.Serialize(methodInfo));

        public bool IsWhitelisted(IPropertySymbol propertyInfo)
            => IsWhitelisted(MethodSymbolSerializer.Serialize(propertyInfo));

        public bool IsWhitelisted(string symbolName)
            => _whitelist?.Contains(symbolName) ?? false;
    }
}
