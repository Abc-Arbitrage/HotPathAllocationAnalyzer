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
                
                public void CreateString1() {
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
                public void CreateString1() {
                    string str = new string('a', 5);
                }";

            var analyser = new ExplicitAllocationAnalyzer();
            
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.ObjectInitializerExpression));
            Assert.AreEqual(1, info.Allocations.Count);
        }
    }
}