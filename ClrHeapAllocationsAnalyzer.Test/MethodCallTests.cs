using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClrHeapAllocationAnalyzer.Test
{
    [TestClass]
    public class MethodCallTests : AllocationAnalyzerTests
    {
        [TestMethod]
        public void AnalyzeProgram_NotAllowCallingOwnedUnflagedMethod()
        {
            //language=cs
            const string sample =
                @"using System;
                using ClrHeapAllocationAnalyzer;
                
                public string CreateString() {
                    return new string('a', 5);
                }

                [ClrHeapAllocationAnalyzer.RestrictedAllocation]
                public void PerfCritical() {
                    string str = CreateString();
                }";

            var analyser = new MethodCallAnalyzer();
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression));
            Assert.AreEqual(1, info.Allocations.Count);
        }      
        
        [TestMethod]
        public void AnalyzeProgram_MethodShouldOnlyBeAllowedToCallNonAllocatingMethods()
        {
            //language=cs
            const string sample =
                @"using System;
                using ClrHeapAllocationAnalyzer;
                
                [ClrHeapAllocationAnalyzer.RestrictedAllocation]
                public int StringLength(string str) {
                    return str.Length;
                }

                [ClrHeapAllocationAnalyzer.RestrictedAllocation]
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
                using ClrHeapAllocationAnalyzer;

                interface IFoo
                {
                    [ClrHeapAllocationAnalyzer.RestrictedAllocation]
                    int StringLength(string str);
                }

                public class Foo : IFoo
                {
                     public int StringLength(string str)
                     {
                        return str.Length;
                     }
                }
                
                [ClrHeapAllocationAnalyzer.RestrictedAllocation]
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
                using ClrHeapAllocationAnalyzer;
                
                [ClrHeapAllocationAnalyzer.RestrictedAllocation]
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
                using ClrHeapAllocationAnalyzer;
                
                [ClrHeapAllocationAnalyzer.RestrictedAllocation]
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
                using ClrHeapAllocationAnalyzer;
                
                [ClrHeapAllocationAnalyzer.RestrictedAllocation]
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
                using ClrHeapAllocationAnalyzer;
                
                [ClrHeapAllocationAnalyzer.RestrictedAllocation]
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
                using ClrHeapAllocationAnalyzer;
                
                public class Config : IAllocationConfiguration 
                {
                    public void String(string str) 
                    {
                        MakeSafe(() => str.IsNormalized());
                        MakeSafe(() => str.Contains(default));
                    } 
                }
                
                [ClrHeapAllocationAnalyzer.RestrictedAllocation]
                public bool PerfCritical(string str) {
                    return str.IsNormalized();
                }";
            
            var analyser = new MethodCallAnalyzer();
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression, SyntaxKind.ClassDeclaration));
            Assert.AreEqual(0, info.Allocations.Count);
        }

        [TestMethod]
        public void AnalyzeProgram_NotAllowCallingExternalMethod_UnlessItIsWhitelisted_ByConvetionProject()
        {
            //language=cs
            const string sample =
                @"using System;
                using ClrHeapAllocationAnalyzer;                
                
                [ClrHeapAllocationAnalyzer.RestrictedAllocation]
                public bool PerfCritical(string str) {
                    return str.IsNormalized();
                }";
            
            var analyser = new MethodCallAnalyzer();
            var currentFilePath = new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileName();
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression), filePath: currentFilePath);
            Assert.AreEqual(0, info.Allocations.Count);
        }
    }
}
