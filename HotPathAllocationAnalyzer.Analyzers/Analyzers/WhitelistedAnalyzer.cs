using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HotPathAllocationAnalyzer.Analyzers;

public abstract class WhitelistedAnalyzer : AllocationAnalyzer
{
    public WhitelistedAnalyzer()
    {
        
    }

    public WhitelistedAnalyzer(bool forceEnableAnalysis)
        : base(forceEnableAnalysis)
    {
        
    }
    
    protected bool _whitelistFound = false;
    protected readonly HashSet<string> _whitelistedSymbols = new();

    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1025:Configure generated code analysis")]
    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1026:Enable concurrent execution")]
    public override void Initialize(AnalysisContext context)
    {
        base.Initialize(context);
        context.RegisterCompilationStartAction(LoadWhitelistedSymbols);
    }

    private void LoadWhitelistedSymbols(CompilationStartAnalysisContext context)
    {
        var additionalFiles = context.Options.AdditionalFiles;
        var whitelistFile = additionalFiles.FirstOrDefault(x => string.Equals(Path.GetFileName(x.Path), "whitelist.txt", StringComparison.OrdinalIgnoreCase));
        if (whitelistFile == null)
            return;
        _whitelistFound = true;
        _whitelistedSymbols.UnionWith(whitelistFile.GetText(context.CancellationToken)
                                                   ?.Lines.Select(x => x.ToString())
                                                   .Where(x => !x.StartsWith("#"))
                                      ?? Array.Empty<string>());
    }
        
    public void AddToWhiteList(string method)
    {
        _whitelistedSymbols.Add(method);
    }
        
    protected void ReportError(SyntaxNodeAnalysisContext context, SyntaxNode node, string name, DiagnosticDescriptor diagnosticDescriptor, Action<string> logger)
    {
        var details = $"[{node} / {name}]";
        if (!_whitelistFound)
            details += " (no whitelist found)";
        else if (_whitelistedSymbols.Count == 0)
            details += $" (empty whitelist)";
            
        context.ReportDiagnostic(Diagnostic.Create(diagnosticDescriptor, node.GetLocation(), details));
        logger.Invoke(node.SyntaxTree.FilePath);
    }
        
}
