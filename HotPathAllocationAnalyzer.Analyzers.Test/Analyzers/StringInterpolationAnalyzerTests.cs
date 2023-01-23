using System.Collections.Immutable;
using HotPathAllocationAnalyzer.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HotPathAllocationAnalyzer.Test.Analyzers
{
    [TestClass]
    public class StringInterpolationAnalyzerTests : AllocationAnalyzerTests
    {
        [TestMethod]
        public void StringInterpolation_ShouldIgnoreWhitelistedHandler()
        {
            //language=cs
            const string sample = @"
            using System;
            using System.Collections.Generic;

            public void Testing() {
                var buffer = new char[100];
                var v = buffer.AsSpan().TryWrite($""ABC{123}X"", out var _); //does not allocate
                Log($""Hello {123}""); //allocate
                var name = ""Foo"";
                var msg = $""Hello {name}""; //allocate
            }

            private static void Log(string error)
            {
                
            }
";
            var analyser = new StringInterpolationAnalyzer(true);
            analyser.AddToWhiteList("System.MemoryExtensions.TryWriteInterpolatedStringHandler");
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InterpolatedStringExpression));

            Assert.AreEqual(2, info.Allocations.Count);
        }
        
        [TestMethod]
        public void StringInterpolation_ShouldAllocateForUnknownHandler()
        {
            //language=cs
            const string sample = @"
            using System;
            using System.Collections.Generic;

            public void Testing() {
                var buffer = new char[100];
                var v = buffer.AsSpan().TryWrite($""ABC{123}X"", out var _); //does not allocate
                Log($""Hello {123}""); //allocate
                var name = ""Foo"";
                var msg = $""Hello {name}""; //allocate
            }

            private static void Log(string error)
            {
                
            }
";
            var analyser = new StringInterpolationAnalyzer(true);
            var info = ProcessCode(analyser, sample, ImmutableArray.Create(SyntaxKind.InterpolatedStringExpression));

            Assert.AreEqual(3, info.Allocations.Count);
        }
    }    
}

