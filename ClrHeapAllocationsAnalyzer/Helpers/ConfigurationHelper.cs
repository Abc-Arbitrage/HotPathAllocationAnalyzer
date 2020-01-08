using System;
using System.IO;
using System.Linq;
using ClrHeapAllocationAnalyzer.Support;

internal static class ConfigurationHelper
{
    public static void ReadConfiguration(string filePath, Action<string> AddToWhiteList)
    {
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
}