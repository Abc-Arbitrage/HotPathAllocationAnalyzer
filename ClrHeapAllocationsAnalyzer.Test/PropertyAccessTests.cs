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
                
                [ClrHeapAllocationAnalyzer.Support.RestrictedAllocation]
                public int PerfCritical(string str) {
                    return str.Length;
                }";

            var analyser = new MethodCallAnalyzer();
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression));
            Assert.AreEqual(1, info.Allocations.Count);
        }    
        
        [TestMethod]
        public void AnalyzeProgram_AllowCallingSafePropertyGetter()
        {
            //language=cs
            const string sample =
                @"using System;
                using System.Collections.Generic;
                using ClrHeapAllocationAnalyzer;
                
                public class Foo
                {
                    public List<int> Data { [ClrHeapAllocationAnalyzer.Support.RestrictedAllocation] get; } = new List<int>();
                }
                
                [ClrHeapAllocationAnalyzer.Support.RestrictedAllocation]
                public List<int> PerfCritical(Foo foo) {
                    return foo.Data;
                }";

            var analyser = new ExplicitAllocationAnalyzer();
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.ObjectCreationExpression));
            Assert.AreEqual(0, info.Allocations.Count);
        }
        
        [TestMethod]
        public void AnalyzeProgram_NotAllowCallingUnSafePropertyGetter()
        {
            //language=cs
            const string sample =
                @"using System;
                using System.Collections.Generic;
                using ClrHeapAllocationAnalyzer;
                
                public class Foo
                {
                    public List<int> Data { [ClrHeapAllocationAnalyzer.Support.RestrictedAllocation] get { return new List<int>(); } }
                }
                
                [ClrHeapAllocationAnalyzer.Support.RestrictedAllocation]
                public List<int> PerfCritical(Foo foo) {
                    return foo.Data;
                }";

            var analyser = new ExplicitAllocationAnalyzer();
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.ObjectCreationExpression));
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
                    string Name { [ClrHeapAllocationAnalyzer.Support.RestrictedAllocation] get; }
                }            
                    
                public class Foo : IFoo
                {                
                    public string Name { get; }
                }
                
                [ClrHeapAllocationAnalyzer.Support.RestrictedAllocation]
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
                using ClrHeapAllocationAnalyzer.Support;
                
                public class FooBase 
                {                
                    public virtual string Name { [ClrHeapAllocationAnalyzer.Support.RestrictedAllocation] get; }
                }            
                    
                public class Foo : FooBase
                {                
                    public override string Name { get; }
                }
                
                [ClrHeapAllocationAnalyzer.Support.RestrictedAllocation]
                public string PerfCritical(Foo f) {
                    return f.Name;
                }";

            var analyser = new MethodCallAnalyzer();
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression));
            Assert.AreEqual(0, info.Allocations.Count);
        }

        [TestMethod]
        public void AnalyzeProgram_AllowCallingGenericWhitelistedProperty()
        {
            //language=cs
            const string sample =
                @"using System;
                using ClrHeapAllocationAnalyzer;

                public class DateProvider
                {
                    public DateTime? Date { [ClrHeapAllocationAnalyzer.Support.RestrictedAllocation] get; }
                }

                [ClrHeapAllocationAnalyzer.Support.RestrictedAllocation]
                public DateTime PerfCritical(DateProvider dp) {
                    return dp.Date.Value;
                }";

            var analyser = new MethodCallAnalyzer();
            analyser.AddToWhiteList("System.Nullable<T>.Value");
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression));
            Assert.AreEqual(0, info.Allocations.Count);
        }
        
        [TestMethod]
        public void AnalyzeProgram_ConsiderAutoPropertyAsSafe()
        {
            //language=cs
            const string sample =
                @"using System;
                using ClrHeapAllocationAnalyzer.Support;
                
                public class Foo
                {
                    public string Name { get; set; }
                }
                
                [ClrHeapAllocationAnalyzer.Support.RestrictedAllocation]
                public string PerfCritical(Foo f) 
                {
                    return f.Name;
                }";

            var analyser = new MethodCallAnalyzer();
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression));
            Assert.AreEqual(0, info.Allocations.Count);
        }
    }
}
