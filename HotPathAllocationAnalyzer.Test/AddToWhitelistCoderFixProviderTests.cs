using System.Collections.Generic;
using HotPathAllocationAnalyzer.Analyzers;
using HotPathAllocationAnalyzer.CodeFix;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace HotPathAllocationAnalyzer.Test
{
    [TestClass]
    public class AddToWhitelistCoderFixProviderTests : CodeFixVerifier
    {
        [TestMethod]
        public void AnalyzeProgram_CodeFixUpdateCodeForMemberAccess()
        {
            //language=cs
            const string sample =
                @"
                    using System;
                    using System.Collections.Generic;
                    using HotPathAllocationAnalyzer.Support;                
                    
                    namespace MyNamespace
                    {
                        public class EventHandler
                        {
                            [HotPathAllocationAnalyzer.Support.RestrictedAllocation]
                            public int Handle<T>(List<T> events)
                            {
                                return events.Count; 
                            }
                        }
                    }
                ";
            
            //language=cs
            const string expectedFix =
@"using System;
using System.Collections.Generic;
using HotPathAllocationAnalyzer.Support;

namespace MyNamespace
{
    public class EventHandler
    {
        [HotPathAllocationAnalyzer.Support.RestrictedAllocation]
        public int Handle<T>(List<T> events)
        {
            return events.Count;
        }
    }

    public class MyConfiguration : AllocationConfiguration
    {
        public void WhitelistList<T>(System.Collections.Generic.List<T> arg)
        {
            MakeSafe(() => arg.Count);
        }
    }
}";

            VerifyCSharpFix(sample, expectedFix);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new AddToWhitelistCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new MethodCallAnalyzer();
        }
    }
}
