using System.IO;

namespace HotPathAllocationAnalyzer.Helpers
{
    internal static class ConfigurationHelper
    {
        public static string FindConfigurationDirectory(string filePath)
        {
            var directoryName = Path.GetDirectoryName(filePath);
            while (!string.IsNullOrEmpty(directoryName))
            {
                var configurationDirectory = Path.Combine(directoryName, AllocationRules.ConfigurationDirectoryName);
                if (Directory.Exists(configurationDirectory))
                    return configurationDirectory;

                directoryName = Directory.GetParent(directoryName)?.FullName;
            }

            return null;
        }    
    }
}