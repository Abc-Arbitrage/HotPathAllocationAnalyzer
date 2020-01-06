using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClrHeapAllocationAnalyzer.Test
{
    [TestClass]
    public class IgnoreTests : AllocationAnalyzerTests
    {
        [TestMethod]
        public void AnalyzeProgram_IgnoreWhenThereIsNoRestrictedAttributes()
        {
            const string sample =
                @"using System;
                
                public void CreateString() {
                    string str = new string('a', 5);
                }";
            
            var analyser = new ExplicitAllocationAnalyzer();
           
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.ObjectInitializerExpression));
            Assert.AreEqual(0, info.Allocations.Count);
        }
        
        [TestMethod]
        public void AnalyzeProgram_AnalyzeWhenThereIsARestrictedAttributes()
        {
            const string sample =
                @"using System;
                using ClrHeapAllocationAnalyzer;

                [ClrHeapAllocationAnalyzer.RestrictedAllocation]
                public void CreateString() {
                    string str = new string('a', 5);
                }";

            var analyser = new ExplicitAllocationAnalyzer();
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.ObjectInitializerExpression));
            Assert.AreEqual(1, info.Allocations.Count);
        }        
        
        [TestMethod]
        public void AnalyzeProgram_NotAllowCallingOwnedUnflagedMethod()
        {
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
            const string sample =
                @"using System;
                using ClrHeapAllocationAnalyzer;
                
                [ClrHeapAllocationAnalyzer.RestrictedAllocation]
                public string PerfCritical(string str) {
                    return string.Copy(str);
                }";

            var analyser = new ExplicitAllocationAnalyzer();
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InvocationExpression));
            Assert.AreEqual(1, info.Allocations.Count);
        }
        
        [TestMethod]
        public void AnalyzeProgram_NotAllowCallingExternalMethod_UnlessItIsWhitelisted()
        {
            const string sample =
                @"using System;
                using ClrHeapAllocationAnalyzer;
                
                [ClrHeapAllocationAnalyzer.RestrictedAllocation]
                public int PerfCritical(string str) {
                    return str.Length;
                }";

            var analyser = new ExplicitAllocationAnalyzer();
            
            // TODO Add to whitelist
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.ObjectInitializerExpression));
            Assert.AreEqual(1, info.Allocations.Count);
        }
        
        [TestMethod]
        public void AnalyzeProgram_NotAllowCallingExternalMethod_UnlessItIsInSafeScope()
        {
            const string sample =
                @"using System;
                using ClrHeapAllocationAnalyzer;
                
                [ClrHeapAllocationAnalyzer.RestrictedAllocation]
                public int PerfCritical(string str) {
                    using (new AllocationFreeScope());
                    return str.Length;
                }";

            var analyser = new ExplicitAllocationAnalyzer();
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.ObjectInitializerExpression));
            Assert.AreEqual(1, info.Allocations.Count);
        }
        
        [TestMethod]
        public void AnalyzeProgram_AnalyzeImplementationWhenInterfaceHasRestrictedAttributes()
        {
            const string sample =
                @"using System;
                using ClrHeapAllocationAnalyzer;

                interface IFoo     
                {
                    [ClrHeapAllocationAnalyzer.RestrictedAllocation]
                    void CreateString();
                }

                class Foo : IFoo 
                {
                    public void CreateString() {
                        string str = new string('a', 5);
                    }
                }";

            var analyser = new ExplicitAllocationAnalyzer();
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.ObjectInitializerExpression));
            Assert.AreEqual(1, info.Allocations.Count);
        }    
        
        [TestMethod]
        public void AnalyzeProgram_AnalyzeExplicitImplementationWhenInterfaceHasRestrictedAttributes()
        {
            const string sample =
                @"using System;
                using ClrHeapAllocationAnalyzer;

                interface IFoo     
                {
                    [ClrHeapAllocationAnalyzer.RestrictedAllocation]
                    void CreateString();
                }

                class Foo : IFoo 
                {
                    void IFoo.CreateString() {
                        string str = new string('a', 5);
                    }
                }";

            var analyser = new ExplicitAllocationAnalyzer();
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.ObjectInitializerExpression));
            Assert.AreEqual(1, info.Allocations.Count);
        }
        
        [TestMethod]
        public void AnalyzeProgram_AnalyzeImplementationWhenBaseHasRestrictedAttributes()
        {
            const string sample =
                @"using System;
                using ClrHeapAllocationAnalyzer;

                class BaseFoo     
                {
                    [ClrHeapAllocationAnalyzer.RestrictedAllocation]
                    public virtual void CreateString() {}
                }

                class Foo : BaseFoo 
                {
                    public override void CreateString() {
                        string str = new string('a', 5);
                    }
                }";

            var analyser = new ExplicitAllocationAnalyzer();
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.ObjectInitializerExpression));
            Assert.AreEqual(1, info.Allocations.Count);
        }        
        
        [TestMethod]
        public void AnalyzeProgram_AnalyzeImplementationWhenRootHasRestrictedAttributes()
        {
            const string sample =
                @"using System;
                using ClrHeapAllocationAnalyzer;

                class BaseFoo     
                {
                    [ClrHeapAllocationAnalyzer.RestrictedAllocation]
                    public virtual void CreateString() {}
                }

                class IntermediateFoo : BaseFoo 
                {
                    public override void CreateString() {}
                }

                class LeafFoo : IntermediateFoo 
                {
                    public override void CreateString() {
                        string str = new string('a', 5);
                    }
                }";

            var analyser = new ExplicitAllocationAnalyzer();
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.ObjectInitializerExpression));
            Assert.AreEqual(1, info.Allocations.Count);
        }
        
        [TestMethod]
        public void AnalyzeProgram_AnalyzeImplementationWhenRootInterfaceHasRestrictedAttributes()
        {
            const string sample =
                @"using System;
                using ClrHeapAllocationAnalyzer;

                interface IFoo     
                {
                    [ClrHeapAllocationAnalyzer.RestrictedAllocation]
                    void CreateString();
                }

                abstract class FooBase : IFoo 
                {
                    public abstract void CreateString();
                }

                class Foo : FooBase 
                {
                    public override void CreateString() {
                        string str = new string('a', 5);
                    }
                }";

            var analyser = new ExplicitAllocationAnalyzer();
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.ObjectInitializerExpression));
            Assert.AreEqual(1, info.Allocations.Count);
        }
    }
}