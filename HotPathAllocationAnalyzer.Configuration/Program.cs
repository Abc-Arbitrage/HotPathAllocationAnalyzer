﻿using System;
using System.IO;
using System.Linq;
using System.Threading;
using HotPathAllocationAnalyzer.Helpers;

namespace HotPathAllocationAnalyzer.Configuration
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
            var whiteList = ConfigurationReader.GenerateDisclaimer().Concat(configurationReader.GenerateWhitelistAsync(cancellationTokenSource.Token).Result);

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
