﻿using System.Collections.Immutable;
using System.Linq;
using HotPathAllocationAnalyzer.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HotPathAllocationAnalyzer.Test.Analyzers
{
    [TestClass]
    public class ConcatenationAllocationAnalyzerTests : AllocationAnalyzerTests {
        
        
        [TestMethod]
        public void ConcatenationAllocation_Basic() {
            var snippet0 = @"var s0 = ""hello"" + 0.ToString() + ""world"" + 1.ToString();";
            var snippet1 = @"var s2 = ""ohell"" + 2.ToString() + ""world"" + 3.ToString() + 4.ToString();";

            var analyser = new ConcatenationAllocationAnalyzer(true);
            var info0 = ProcessCode(analyser, snippet0, ImmutableArray.Create(SyntaxKind.AddExpression, SyntaxKind.AddAssignmentExpression));
            var info1 = ProcessCode(analyser, snippet1, ImmutableArray.Create(SyntaxKind.AddExpression, SyntaxKind.AddAssignmentExpression));

            //should raise once for every binary expression
            Assert.AreEqual(3, info0.Allocations.Count(d => d.Id == ConcatenationAllocationAnalyzer.StringConcatenationAllocationRule.Id));
            Assert.AreEqual(4, info1.Allocations.Count(d => d.Id == ConcatenationAllocationAnalyzer.StringConcatenationAllocationRule.Id));
        }
        
        [TestMethod]
        public void ConcatenationAllocation_DoNotWarnForOptimizedValueTypes() {
            var snippets = new[]
            {
                @"string s0 = nameof(System.String) + '-';",
                @"string s0 = nameof(System.String) + true;",
                @"string s0 = nameof(System.String) + new System.IntPtr();",
                @"string s0 = nameof(System.String) + new System.UIntPtr();"
            };

            var analyser = new ConcatenationAllocationAnalyzer(true);
            foreach (var snippet in snippets) {
                var info = ProcessCode(analyser, snippet, ImmutableArray.Create(SyntaxKind.AddExpression, SyntaxKind.AddAssignmentExpression));
                Assert.AreEqual(0, info.Allocations.Count(x => x.Id == ConcatenationAllocationAnalyzer.ValueTypeToReferenceTypeInAStringConcatenationRule.Id));
            }
        }
        
        [TestMethod]
        public void ConcatenationAllocation_DoNotWarnForConst() {
            var snippets = new[]
            {
                @"const string s0 = nameof(System.String) + ""."" + nameof(System.String);",
                @"const string s0 = nameof(System.String) + ""."";",
                @"string s0 = nameof(System.String) + ""."" + nameof(System.String);",
                @"string s0 = nameof(System.String) + ""."";"
            };

            var analyser = new ConcatenationAllocationAnalyzer(true);
            foreach (var snippet in snippets) {
                var info = ProcessCode(analyser, snippet, ImmutableArray.Create(SyntaxKind.AddExpression, SyntaxKind.AddAssignmentExpression));
                Assert.AreEqual(0, info.Allocations.Count);
            }
        }
    }
}