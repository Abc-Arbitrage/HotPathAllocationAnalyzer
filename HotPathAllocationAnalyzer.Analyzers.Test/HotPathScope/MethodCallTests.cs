using System.Collections.Immutable;
using HotPathAllocationAnalyzer.Analyzers;
using HotPathAllocationAnalyzer.Test.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HotPathAllocationAnalyzer.Test.HotPathScope
{
    [TestClass]
    public class MethodCallTests : AllocationAnalyzerTests
    {
        [TestMethod]
        public void AnalyzeProgram_NotAllowCallingOwnedUnFlaggedMethod()
        {
            //language=cs
            const string sample =
                @"using System;
                using HotPathAllocationAnalyzer.Support;
                
                public string CreateString() {
                    return new string('a', 5);
                }

                [NoAllocation]
                public void PerfCritical() {
                    string str = CreateString();
                }";

            var analyser = new MethodCallAnalyzer();

            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression));
            Assert.AreEqual(1, info.Allocations.Count);
        }

        [TestMethod]
        public void AnalyzeProgram_FlagMethodWhenClassIsFlagged()
        {
            //language=cs
            const string sample =
                @"using System;
                using HotPathAllocationAnalyzer.Support;
                
                [NoAllocation]
                public class CriticalClass {
                    public string CreateString() {
                        return null;
                    }
                }

                [NoAllocation]
                public void PerfCritical(CriticalClass c) {
                    string str = c.CreateString();
                }";

            var analyser = new MethodCallAnalyzer();

            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression));
            Assert.AreEqual(0, info.Allocations.Count);
        }

        [TestMethod]
        public void AnalyzeProgram_FlagMethodWhenBaseClassIsFlagged()
        {
            //language=cs
            const string sample =
                @"using System;
                using HotPathAllocationAnalyzer.Support;
                
                [NoAllocation]
                public class CriticalBaseClass {
                }
                
                public class CriticalBaseClassEx : CriticalBaseClass {
                }
                
                public class CriticalClass : CriticalBaseClassEx {
                    public string CreateString() {
                        return null;
                    }
                }

                [NoAllocation]
                public void PerfCritical(CriticalClass c) {
                    string str = c.CreateString();
                }";

            var analyser = new MethodCallAnalyzer();

            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression));
            Assert.AreEqual(0, info.Allocations.Count);
        }

        [TestMethod]
        public void AnalyzeProgram_MethodShouldOnlyBeAllowedToCallNonAllocatingMethods()
        {
            //language=cs
            const string sample =
                @"using System;
                using HotPathAllocationAnalyzer.Support;
                
                [NoAllocation]
                public int StringLength(string str) {
                    return 0;
                }

                [NoAllocation]
                public void PerfCritical(string str) {
                    int l = StringLength(str);
                }";

            var analyser = new MethodCallAnalyzer();

            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression));
            Assert.AreEqual(0, info.Allocations.Count);
        }

        [TestMethod]
        public void AnalyzeProgram_MethodShouldOnlyBeAllowedToCallNonAllocatingMethodsOnNonAllocatingInterface()
        {
            //language=cs
            const string sample =
                @"using System;
                using HotPathAllocationAnalyzer.Support;

                interface IFoo
                {
                    [NoAllocation]
                    int StringLength(string str);
                }

                public class Foo : IFoo
                {
                     public int StringLength(string str)
                     {
                        return 0;
                     }
                }
                
                [NoAllocation]
                public void PerfCritical(Foo foo, string str)
                {
                    int l = foo.StringLength(str);
                }";

            var analyser = new MethodCallAnalyzer();

            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression));
            Assert.AreEqual(0, info.Allocations.Count);
        }

        [TestMethod]
        public void AnalyzeProgram_NotAllowCallingExternalMethod()
        {
            //language=cs
            const string sample =
                @"using System;
                using HotPathAllocationAnalyzer.Support;
                
                [NoAllocation]
                public string PerfCritical(string str) {
                    return string.Copy(str);
                }";

            var analyser = new MethodCallAnalyzer();

            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression));
            Assert.AreEqual(1, info.Allocations.Count);
        }

        [TestMethod]
        public void AnalyzeProgram_NotAllowCallingExternalMethod_UnlessItIsInSafeScope()
        {
            //language=cs
            const string sample =
                @"using System;
                using HotPathAllocationAnalyzer.Support;
                
                [NoAllocation]
                public bool PerfCritical(string str)
                 {
                    using (new AllocationFreeScope())
                    {
                        return str.IsNormalized();
                    }
                }";

            var analyser = new MethodCallAnalyzer();

            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression));
            Assert.AreEqual(0, info.Allocations.Count);
        }

        [TestMethod]
        public void AnalyzeProgram_NotAllowCallingExternalMethod_UnlessItIsInSafeScope2()
        {
            //language=cs
            const string sample =
                @"using System;
                using HotPathAllocationAnalyzer.Support;
                
                [NoAllocation]
                public bool PerfCritical(string str)
                {                                     
                    using var safeScope = new AllocationFreeScope();
                    
                    return str.IsNormalized();                    
                }";

            var analyser = new MethodCallAnalyzer();

            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression));
            Assert.AreEqual(0, info.Allocations.Count);
        }

        [TestMethod]
        public void AnalyzeProgram_NotAllowCallingExternalMethod_UnlessItIsInSafeScope3()
        {
            //language=cs
            const string sample =
                @"using System;
                using HotPathAllocationAnalyzer.Support;
                
                [NoAllocation]
                public bool PerfCritical(string str)
                {                 
                    var result = str.IsNormalized();                    
                    using var safeScope = new AllocationFreeScope();                    
                    return result;                    
                }";

            var analyser = new MethodCallAnalyzer();

            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression));
            Assert.AreEqual(1, info.Allocations.Count);
        }

        [TestMethod]
        public void AnalyzeProgram_NotAllowCallingExternalMethod_UnlessItIsWhitelisted()
        {
            //language=cs
            const string sample =
                @"using System;
                using HotPathAllocationAnalyzer.Support;
                
                [NoAllocation]
                public bool PerfCritical(string str) {
                    return str.Contains(""zig"");
                }";

            var analyser = new MethodCallAnalyzer();

            analyser.AddToWhiteList("string.IsNormalized()");
            analyser.AddToWhiteList("string.Contains(string)");

            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression, SyntaxKind.ClassDeclaration));
            Assert.AreEqual(0, info.Allocations.Count);
        }

        [TestMethod]
        public void AnalyzeProgram_NotAllowCallingExternalMethod_UnlessItIsWhitelisted_ByConventionProject()
        {
            //language=cs
            const string sample =
                @"using System;
                using HotPathAllocationAnalyzer.Support;                
                
                [NoAllocation]
                public bool PerfCritical(string str) {
                    return str.IsNormalized();
                }";

            var analyser = new MethodCallAnalyzer();
            var whitelist = @"
string.Length
string.IsNormalized()
string.Equals(string)";
            var additionalFile = new TestAdditionalFile("whitelist.txt", whitelist);
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression),
                                   additionalFiles: new []{additionalFile});
            Assert.AreEqual(0, info.Allocations.Count);
        }

        [TestMethod]
        public void AnalyzeProgram_AllowCallingGenericWhitelistedMethods()
        {
            //language=cs
            const string sample =
                @"
                    using System;
                    using System.Collections.Generic;
                    using HotPathAllocationAnalyzer.Support;                
                
                    [NoAllocation]
                    public int PerfCritical(List<int> l) {
                        return l.IndexOf(10);
                    }
                ";

            var analyser = new MethodCallAnalyzer();
            analyser.AddToWhiteList("System.Collections.Generic.List<T>.IndexOf(T)");

            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression));
            Assert.AreEqual(0, info.Allocations.Count);
        }

        [TestMethod]
        public void AnalyzeProgram_NotAllowCallingNonGenericWhitelistedMethods()
        {
            //language=cs
            const string sample =
                @"
                    using System;
                    using System.Collections.Generic;
                    using HotPathAllocationAnalyzer.Support;                
                
                    [NoAllocation]
                    public int PerfCritical<T>(List<T> l, T val) {
                        return l.IndexOf(val);
                    }
                ";

            var analyser = new MethodCallAnalyzer();
            analyser.AddToWhiteList("System.Collections.Generic.List<double>.IndexOf(double)");

            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression));
            Assert.AreEqual(1, info.Allocations.Count);
        }
    }
}
