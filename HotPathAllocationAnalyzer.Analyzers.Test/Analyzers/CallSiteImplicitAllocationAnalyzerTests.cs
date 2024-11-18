using HotPathAllocationAnalyzer.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HotPathAllocationAnalyzer.Test.Analyzers
{
    [TestClass]
    public class CallSiteImplicitAllocationAnalyzerTests : AllocationAnalyzerTests
    {
        [TestMethod]
        public void CallSiteImplicitAllocation_Param()
        {
            // language=csharp
            const string sampleProgram =
                """
                using System;

                Params(); //no allocation, because compiler will implicitly substitute Array<int>.Empty
                Params(1, 2);
                Params(new [] { 1, 2}); // explicit, so no warning
                ParamsWithObjects(new [] { 1, 2}); // explicit, but converted to objects, so still a warning?!

                // Only 4 args and above use the params overload of String.Format
                var test = String.Format("Testing {0}, {1}, {2}, {3}", 1, "blah", 2.0m, 'c');

                public void Params(params int[] args)
                {
                }

                public void ParamsWithObjects(params object[] args)
                {
                }
                """;

            var analyser = new CallSiteImplicitAllocationAnalyzer(true);
            var info = ProcessCode(analyser, sampleProgram, [SyntaxKind.InvocationExpression]);

            Assert.AreEqual(3, info.Allocations.Count, "Should report 3 allocations");
            // Diagnostic: (4,1): warning HeapAnalyzerImplicitParamsRule: This call site is calling into a function with a 'params' parameter. This results in an array allocation
            AssertEx.ContainsDiagnostic(info.Allocations, id: CallSiteImplicitAllocationAnalyzer.ParamsParameterRule.Id, line: 4, character: 1);
            // Diagnostic: (6,1): warning HeapAnalyzerImplicitParamsRule: This call site is calling into a function with a 'params' parameter. This results in an array allocation
            AssertEx.ContainsDiagnostic(info.Allocations, id: CallSiteImplicitAllocationAnalyzer.ParamsParameterRule.Id, line: 6, character: 1);
            // Diagnostic: (9,12): warning HeapAnalyzerImplicitParamsRule: This call site is calling into a function with a 'params' parameter. This results in an array allocation
            AssertEx.ContainsDiagnostic(info.Allocations, id: CallSiteImplicitAllocationAnalyzer.ParamsParameterRule.Id, line: 9, character: 12);
        }

        [TestMethod]
        public void CallSiteImplicitAllocation_NonOverridenMethodOnStruct()
        {
            // language=csharp
            const string sampleProgram =
                """
                using System;

                var normal = new Normal().GetHashCode();
                var overridden = new OverrideToHashCode().GetHashCode();

                struct Normal
                {

                }

                struct OverrideToHashCode
                {
                
                    public override int GetHashCode()
                    {
                        return -1;
                    }
                }
                """;

            var analyser = new CallSiteImplicitAllocationAnalyzer(true);
            var info = ProcessCode(analyser, sampleProgram, [SyntaxKind.InvocationExpression]);

            Assert.AreEqual(1, info.Allocations.Count);
            // Diagnostic: (3,14): warning HeapAnalyzerValueTypeNonOverridenCallRule: Non-overriden virtual method call on a value type adds a boxing or constrained instruction
            AssertEx.ContainsDiagnostic(info.Allocations, id: CallSiteImplicitAllocationAnalyzer.ValueTypeNonOverridenCallRule.Id, line: 3, character: 14);
        }

        [TestMethod]
        public void CallSiteImplicitAllocation_DoNotReportNonOverriddenMethodCallForStaticCalls()
        {
            var snippet = @"var t = System.Enum.GetUnderlyingType(typeof(System.StringComparison));";

            var analyser = new CallSiteImplicitAllocationAnalyzer(true);
            var info = ProcessCode(analyser, snippet, [SyntaxKind.InvocationExpression]);

            Assert.AreEqual(0, info.Allocations.Count);
        }

        [TestMethod]
        public void CallSiteImplicitAllocation_DoNotReportNonOverriddenMethodCallForNonVirtualCalls()
        {
            // language=csharp
            const string snippet =
                """
                using System.IO;

                FileAttributes attr = FileAttributes.System;
                attr.HasFlag (FileAttributes.Directory);
                """;

            var analyser = new CallSiteImplicitAllocationAnalyzer(true);
            var info = ProcessCode(analyser, snippet, [SyntaxKind.InvocationExpression]);

            Assert.AreEqual(0, info.Allocations.Count);
        }

        [TestMethod]
        public void ParamsIsPrecededByOptionalParameters()
        {
            // language=csharp
            const string sampleProgram =
                """
                using System.IO;

                public class MyClass
                {
                    static class Demo
                    {
                        static void Fun1()
                        {
                            Fun2();
                            Fun2(args: "", i: 5);
                        }
                        static void Fun2(int i = 0, params object[] args)
                        {
                        }
                    }
                }
                """;

            var analyser = new CallSiteImplicitAllocationAnalyzer(true);
            var info = ProcessCode(analyser, sampleProgram, [SyntaxKind.InvocationExpression]);

            Assert.AreEqual(1, info.Allocations.Count, "Should report 1 allocation.");
            // Diagnostic: (10,13): warning HeapAnalyzerImplicitParamsRule: This call site is calling into a function with a 'params' parameter. This results in an array allocation
            AssertEx.ContainsDiagnostic(info.Allocations, id: CallSiteImplicitAllocationAnalyzer.ParamsParameterRule.Id, line: 10, character: 13);
        }
    }
}
