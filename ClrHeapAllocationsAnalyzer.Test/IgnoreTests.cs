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