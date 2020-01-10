using System.Collections.Immutable;
using HotPathAllocationAnalyzer.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HotPathAllocationAnalyzer.Test
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
                using HotPathAllocationAnalyzer.Support;

                [HotPathAllocationAnalyzer.Support.RestrictedAllocation]
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
            //language=cs
            const string sample =
                @"using System;
                using HotPathAllocationAnalyzer.Support;

                interface IFoo     
                {
                    [HotPathAllocationAnalyzer.Support.RestrictedAllocation]
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
            //language=cs
            const string sample =
                @"using System;
                using HotPathAllocationAnalyzer.Support;

                interface IFoo     
                {
                    [HotPathAllocationAnalyzer.Support.RestrictedAllocation]
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
            //language=cs
            const string sample =
                @"using System;
                using HotPathAllocationAnalyzer.Support;

                class BaseFoo     
                {
                    [HotPathAllocationAnalyzer.Support.RestrictedAllocation]
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
            //language=cs
            const string sample =
                @"using System;
                using HotPathAllocationAnalyzer.Support;

                class BaseFoo     
                {
                    [HotPathAllocationAnalyzer.Support.RestrictedAllocation]
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
            //language=cs
            const string sample =
                @"using System;
                using HotPathAllocationAnalyzer.Support;

                interface IFoo     
                {
                    [HotPathAllocationAnalyzer.Support.RestrictedAllocation]
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