using System.Collections.Immutable;
using HotPathAllocationAnalyzer.Analyzers;
using HotPathAllocationAnalyzer.Test.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HotPathAllocationAnalyzer.Test.HotPathScope
{
    [TestClass]
    public class PropertyAccessTests : AllocationAnalyzerTests
    {
        [TestMethod]
        public void AnalyzeProgram_NotAllowCallingPropertyGetter()
        {
            //language=cs
            const string sample =
                """
                using System;
                using HotPathAllocationAnalyzer.Support;
                
                [NoAllocation]
                public int PerfCritical(string str) {
                    return str.Length;
                }
                """;

            var analyser = new MethodCallAnalyzer();
            
            var info = ProcessCode(analyser, sample, [SyntaxKind.InvocationExpression]);
            Assert.AreEqual(1, info.Allocations.Count);
        }    
        
        [TestMethod]
        public void AnalyzeProgram_AllowCallingSafePropertyGetter()
        {
            //language=cs
            const string sample =
                """
                using System;
                using System.Collections.Generic;
                using HotPathAllocationAnalyzer.Support;
                
                public class Foo
                {
                    public List<int> Data { [NoAllocation] get; } = new List<int>();
                }
                
                [NoAllocation]
                public List<int> PerfCritical(Foo foo) {
                    return foo.Data;
                }
                """;

            var analyser = new ExplicitAllocationAnalyzer();
            
            var info = ProcessCode(analyser, sample, [SyntaxKind.ObjectCreationExpression]);
            Assert.AreEqual(0, info.Allocations.Count);
        }
        
        [TestMethod]
        public void AnalyzeProgram_NotAllowCallingUnSafePropertyGetter()
        {
            //language=cs
            const string sample =
                """
                using System;
                using System.Collections.Generic;
                using HotPathAllocationAnalyzer.Support;
                
                public class Foo
                {
                    public List<int> Data { [NoAllocation] get { return new List<int>(); } }
                }
                
                [NoAllocation]
                public List<int> PerfCritical(Foo foo) {
                    return foo.Data;
                }
                """;

            var analyser = new ExplicitAllocationAnalyzer();
            
            var info = ProcessCode(analyser, sample, [SyntaxKind.ObjectCreationExpression]);
            Assert.AreEqual(1, info.Allocations.Count);
        }
                
        [TestMethod]
        public void AnalyzeProgram_AllowCallingFlaggedPropertyGetter_Interface()
        {
            //language=cs
            const string sample =
                """
                using System;
                using HotPathAllocationAnalyzer.Support;
                
                public interface IFoo 
                {                
                    string Name { [NoAllocation] get; }
                }            
                    
                public class Foo : IFoo
                {                
                    public string Name { get; }
                }
                
                [NoAllocation]
                public string PerfCritical(Foo f) {
                    return f.Name;
                }
                """;

            var analyser = new MethodCallAnalyzer();
            
            var info = ProcessCode(analyser, sample, [SyntaxKind.InvocationExpression]);
            Assert.AreEqual(0, info.Allocations.Count);
        }        
        
        [TestMethod]
        public void AnalyzeProgram_AllowCallingFlaggedPropertyGetter_Override()
        {
            //language=cs
            const string sample =
                """
                using System;
                using HotPathAllocationAnalyzer.Support;
                
                public class FooBase 
                {                
                    public virtual string Name { [NoAllocation] get; }
                }            
                    
                public class Foo : FooBase
                {                
                    public override string Name { get; }
                }
                
                [NoAllocation]
                public string PerfCritical(Foo f) {
                    return f.Name;
                }
                """;

            var analyser = new MethodCallAnalyzer();
            
            var info = ProcessCode(analyser, sample, [SyntaxKind.InvocationExpression]);
            Assert.AreEqual(0, info.Allocations.Count);
        }

        [TestMethod]
        public void AnalyzeProgram_AllowCallingGenericWhitelistedProperty()
        {
            //language=cs
            const string sample =
                """
                using System;
                using HotPathAllocationAnalyzer.Support;

                public class DateProvider
                {
                    public DateTime? Date { [NoAllocation] get; }
                }

                [NoAllocation]
                public DateTime PerfCritical(DateProvider dp) {
                    return dp.Date.Value;
                }
                """;

            var analyser = new MethodCallAnalyzer();
            analyser.AddToWhiteList("System.Nullable<T>.Value");
            var info = ProcessCode(analyser, sample, [SyntaxKind.InvocationExpression]);
            Assert.AreEqual(0, info.Allocations.Count);
        }
        
        [TestMethod]
        public void AnalyzeProgram_ConsiderAutoPropertyAndFieldsAsSafe()
        {
            //language=cs
            const string sample =
                """
                using System;
                using HotPathAllocationAnalyzer.Support;
                
                public abstract class FooBase
                {
                    public string FullName;
                    public string Name { get; } = "Hello";
                    public int[] Data { get; } = new int[10];
                }
                
                public class Foo : FooBase
                {
                    public int[] Data { get; } = new int[10];
                }
                
                [NoAllocation]
                public int PerfCritical(Foo f) 
                {
                    return f.Name.Length + + f.FullName.Length + f.Data.Length;
                }
                """;

            var analyser = new MethodCallAnalyzer();
            analyser.AddToWhiteList("string.Length");
            analyser.AddToWhiteList("System.Array.Length");
            
            var info = ProcessCode(analyser, sample, [SyntaxKind.InvocationExpression]);
            Assert.AreEqual(0, info.Allocations.Count);
        }
    }
}
