using System.Collections.Immutable;
using ClrHeapAllocationAnalyzer.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClrHeapAllocationAnalyzer.Test
{
    [TestClass]
    public class PropertyAccessTests : AllocationAnalyzerTests
    {
        [TestMethod]
        public void AnalyzeProgram_NotAllowCallingPropertyGetter()
        {
            //language=cs
            const string sample =
                @"using System;
                using ClrHeapAllocationAnalyzer;
                
                [ClrHeapAllocationAnalyzer.RestrictedAllocation]
                public int PerfCritical(string str) {
                    return str.Length;
                }";

            var analyser = new MethodCallAnalyzer();
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression));
            Assert.AreEqual(1, info.Allocations.Count);
        }
                
        [TestMethod]
        public void AnalyzeProgram_AllowCallingFlaggedPropertyGetter_Interface()
        {
            //language=cs
            const string sample =
                @"using System;
                using ClrHeapAllocationAnalyzer;
                
                public interface IFoo 
                {                
                    string Name { [ClrHeapAllocationAnalyzer.RestrictedAllocation] get; }
                }            
                    
                public class Foo : IFoo
                {                
                    public string Name { get; }
                }
                
                [ClrHeapAllocationAnalyzer.RestrictedAllocation]
                public string PerfCritical(Foo f) {
                    return f.Name;
                }";

            var analyser = new MethodCallAnalyzer();
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression));
            Assert.AreEqual(0, info.Allocations.Count);
        }        
        
        [TestMethod]
        public void AnalyzeProgram_AllowCallingFlaggedPropertyGetter_Override()
        {
            //language=cs
            const string sample =
                @"using System;
                using ClrHeapAllocationAnalyzer;
                
                public class FooBase 
                {                
                    public virtual string Name { [ClrHeapAllocationAnalyzer.RestrictedAllocation] get; }
                }            
                    
                public class Foo : FooBase
                {                
                    public override string Name { get; }
                }
                
                [ClrHeapAllocationAnalyzer.RestrictedAllocation]
                public string PerfCritical(Foo f) {
                    return f.Name;
                }";

            var analyser = new MethodCallAnalyzer();
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression));
            Assert.AreEqual(0, info.Allocations.Count);
        }
    }
}
