
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HotPathAllocationAnalyzer.Configuration.Test
{
    [TestClass]
    public class Tests
    {
        [TestMethod]
        public void GenerateWhitelist()
        {
            var sourceProject = @"C:\Dev\dotnet\src\Analyzers\HotPathAllocationAnalyzer\";
            var reader = new ConfigurationReader(sourceProject);

            var result = reader.GenerateWhitelistAsync(CancellationToken.None).Result;
        }
    }
}
