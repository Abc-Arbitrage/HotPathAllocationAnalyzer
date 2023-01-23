
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HotPathAllocationAnalyzer.Configuration.Test
{
    [TestClass]
    public class Tests
    {
        [TestMethod, Ignore]
        public void GenerateWhitelist()
        {
            var sourceProject = @"C:\Dev\dotnet\src\Analyzers\HotPathAllocationAnalyzer.Analyzers\";
            var reader = new ConfigurationReader(sourceProject);

            var result = reader.GenerateWhitelistAsync(CancellationToken.None).Result;
        }
    }
}
