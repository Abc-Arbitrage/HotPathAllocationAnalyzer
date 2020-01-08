using System;
using System.IO;
using System.Threading;
using ClrHeapAllocationAnalyzer.Helpers;

namespace ClrHeapAllocationAnalyzer.Configuration
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: ConfigureFileGenerator ConfigProjectDirectory [OutputFile]");
                return;
            }
            
            var configurationReader = new ConfigurationReader(args[0]);
            
            var cancellationTokenSource = new CancellationTokenSource();
            var whiteList = configurationReader.GenerateWhitelistAsync(cancellationTokenSource.Token).Result;

            var outputFile = GetOutputFile(args);
            File.WriteAllLines(outputFile, whiteList);
        }

        private static string GetOutputFile(string[] args)
        {
            if (args.Length >= 2)
                return args[1];

            return Path.Combine(args[0], AllocationRules.WhitelistFileName);
        }
    }
}
