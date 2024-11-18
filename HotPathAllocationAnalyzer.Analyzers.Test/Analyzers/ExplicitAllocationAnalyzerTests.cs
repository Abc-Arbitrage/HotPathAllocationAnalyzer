using System.Reflection;
using HotPathAllocationAnalyzer.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HotPathAllocationAnalyzer.Test.Analyzers
{
    [TestClass]
    public class ExplicitAllocationAnalyzerTests : AllocationAnalyzerTests
    {
        [TestMethod]
        public void ExplicitAllocation_InitializerExpressionSyntax()
        {
            // language=csharp
            const string sampleProgram =
                """
                using System;

                var @struct = new TestStruct { Name = "Bob"};
                var @class = new TestClass { Name = "Bob", Age=42 };

                public struct TestStruct
                {
                    public string Name { get; set; }
                }

                public class TestClass
                {
                    public string Name { get; set; }
                    public int Age {get; set;}
                }
                """;

            var analyser = new ExplicitAllocationAnalyzer(true);
            // SyntaxKind.ObjectInitializerExpression IS linked to InitializerExpressionSyntax (naming is a bit confusing)
            var info = ProcessCode(analyser, sampleProgram, [SyntaxKind.ObjectInitializerExpression]);

            Assert.AreEqual(2, info.Allocations.Count);
            // Diagnostic: (4,14): info HeapAnalyzerExplicitNewObjectRule: Explicit new reference type allocation
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.NewObjectRule.Id, line: 4, character: 1);

            // Diagnostic: (4,5): info HeapAnalyzerInitializerCreationRule: Initializer reference type allocation ***
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.InitializerCreationRule.Id, line: 4, character: 28);
        }

        [TestMethod]
        public void ExplicitAllocation_ImplicitArrayCreationExpressionSyntax()
        {
            // language=csharp
            const string sampleProgram =
                """
                using System.Collections.Generic;

                int[] intData = new[] { 123, 32, 4 };
                """;

            var analyser = new ExplicitAllocationAnalyzer(true);
            var info = ProcessCode(analyser, sampleProgram, [SyntaxKind.ImplicitArrayCreationExpression]);

            Assert.AreEqual(1, info.Allocations.Count);
            // Diagnostic: (3,17): info HeapAnalyzerImplicitNewArrayCreationRule: Implicit new array creation allocation
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.ImplicitArrayCreationRule.Id, line: 3, character: 17);
        }

        [TestMethod]
        public void ExplicitAllocation_AnonymousObjectCreationExpressionSyntax()
        {
            // language=csharp
            const string sampleProgram =
                """
                using System;

                var temp = new { A = 123, Name = "Test", };
                """;

            var analyser = new ExplicitAllocationAnalyzer(true);
            var info = ProcessCode(analyser, sampleProgram, [SyntaxKind.AnonymousObjectCreationExpression]);

            Assert.AreEqual(1, info.Allocations.Count);
            // Diagnostic: (3,12): info HeapAnalyzerExplicitNewAnonymousObjectRule: Explicit new anonymous object allocation
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.AnonymousNewObjectRule.Id, line: 3, character: 12);
        }

        [TestMethod]
        public void ExplicitAllocation_ArrayCreationExpressionSyntax()
        {
            // language=csharp
            const string sampleProgram =
                """
                using System.Collections.Generic;

                int[] intData = new int[] { 123, 32, 4 };
                """;

            var analyser = new ExplicitAllocationAnalyzer(true);
            var info = ProcessCode(analyser, sampleProgram, [SyntaxKind.ArrayCreationExpression]);

            Assert.AreEqual(1, info.Allocations.Count);
            // Diagnostic: (3,17): info HeapAnalyzerExplicitNewArrayRule: Implicit new array creation allocation
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.NewArrayRule.Id, line: 3, character: 17);
        }

        [TestMethod]
        public void ExplicitAllocation_ObjectCreationExpressionSyntax()
        {
            // language=csharp
            const string sampleProgram =
                """
                using System;

                var allocation = new String('a', 10);
                var noAllocation = new DateTime();
                """;

            var analyser = new ExplicitAllocationAnalyzer(true);
            var info = ProcessCode(analyser, sampleProgram, [SyntaxKind.ObjectCreationExpression]);

            Assert.AreEqual(1, info.Allocations.Count);
            // Diagnostic: (3,18): info HeapAnalyzerExplicitNewObjectRule: Explicit new reference type allocation
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.NewObjectRule.Id, line: 3, character: 1);
        }

        [TestMethod]
        public void ExplicitAllocation_ObjectCreationExpressionSyntax2()
        {
            // language=csharp
            const string sampleProgram =
                """
                using System;
                using System.Collections.Generic;
                using HotPathAllocationAnalyzer.Support;
                
                    class Foo
                    {
                        [NoAllocation]
                        private static void Bar()
                        {
                            var data = new DateTime();
                        }
                
                        [NoAllocation]
                        private static void Bis()
                        {
                            var data = new List<int>();
                        }
                    }
                """;

            var analyser = new ExplicitAllocationAnalyzer();
            var info = ProcessCode(analyser, sampleProgram, [SyntaxKind.ObjectCreationExpression]);

            Assert.AreEqual(1, info.Allocations.Count);
            // Diagnostic: (3,18): info HeapAnalyzerExplicitNewObjectRule: Explicit new reference type allocation
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.NewObjectRule.Id, line: 16, character: 13);
        }

        [TestMethod]
        public void ExplicitAllocation_LetClauseSyntax()
        {
            // language=csharp
            const string sampleProgram =
                """
                using System.Collections.Generic;
                using System.Linq;

                int[] intData = new[] { 123, 32, 4 };
                var result = (from a in intData
                              let b = a * 3
                              select b).ToList();
                """;

            var analyser = new ExplicitAllocationAnalyzer(true);
            var info = ProcessCode(analyser, sampleProgram, [SyntaxKind.LetClause]);

            Assert.AreEqual(2, info.Allocations.Count);
            // Diagnostic: (4,17): info HeapAnalyzerImplicitNewArrayCreationRule: Implicit new array creation allocation
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.ImplicitArrayCreationRule.Id, line: 4, character: 17);

            // Diagnostic: (6,15): info HeapAnalyzerLetClauseRule: Let clause induced allocation
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.LetCauseRule.Id, line: 6, character: 15);
        }

        [TestMethod]
        public void ExplicitAllocation_AllSyntax()
        {
            // language=csharp
            const string sampleProgram =
                """
                using System;
                using System.Collections.Generic;
                using System.Linq;

                var @struct = new TestStruct { Name = "Bob" };
                var @class = new TestClass { Name = "Bob" };

                int[] intDataImplicit = new[] { 123, 32, 4 };

                var temp = new { A = 123, Name = "Test", };

                int[] intDataExplicit = new int[] { 123, 32, 4 };

                var allocation = new String('a', 10);
                var noAllocation = new DateTime();

                int[] intDataLinq = new int[] { 123, 32, 4 };
                var result = (from a in intDataLinq
                              let b = a * 3
                              select b).ToList();

                public struct TestStruct
                {
                    public string Name { get; set; }
                }

                public class TestClass
                {
                    public string Name { get; set; }
                }
                """;

            // This test is here so that we use SyntaxKindsOfInterest explicitly, to make sure it works
            var analyser = new ExplicitAllocationAnalyzer(true);
            var info = ProcessCode(analyser, sampleProgram, [SyntaxKind.ObjectCreationExpression, SyntaxKind.AnonymousObjectCreationExpression, SyntaxKind.ArrayInitializerExpression, SyntaxKind.CollectionInitializerExpression, SyntaxKind.ComplexElementInitializerExpression, SyntaxKind.ObjectInitializerExpression, SyntaxKind.ArrayCreationExpression, SyntaxKind.ImplicitArrayCreationExpression, SyntaxKind.LetClause]);

            Assert.AreEqual(8, info.Allocations.Count);
            // Diagnostic: (6,14): info HeapAnalyzerExplicitNewObjectRule: Explicit new reference type allocation
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.NewObjectRule.Id, line: 6, character: 1);
            // Diagnostic: (6,5): info HeapAnalyzerInitializerCreationRule: Initializer reference type allocation
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.InitializerCreationRule.Id, line: 6, character: 28);
            // Diagnostic: (8,25): info HeapAnalyzerImplicitNewArrayCreationRule: Implicit new array creation allocation
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.ImplicitArrayCreationRule.Id, line: 8, character: 25);
            // Diagnostic: (10,12): info HeapAnalyzerExplicitNewAnonymousObjectRule: Explicit new anonymous object allocation
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.AnonymousNewObjectRule.Id, line: 10, character: 12);
            // Diagnostic: (12,25): info HeapAnalyzerExplicitNewArrayRule: Explicit new array type allocation
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.NewArrayRule.Id, line: 12, character: 25);
            // Diagnostic: (14,18): info HeapAnalyzerExplicitNewObjectRule: Explicit new reference type allocation
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.NewObjectRule.Id, line: 14, character: 1);
            // Diagnostic: (17,21): info HeapAnalyzerExplicitNewArrayRule: Explicit new array type allocation
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.NewArrayRule.Id, line: 17, character: 21);
            // Diagnostic: (19,15): info HeapAnalyzerLetClauseRule: Let clause induced allocation
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.LetCauseRule.Id, line: 19, character: 15);
        }

        [TestMethod]
        public void ExplicitAllocation_TargetTypeNew()
        {
            // language=csharp
            const string sampleProgram =
                """
                using System;
                using System.Collections.Generic;

                public static class Foo
                {
                    public static int Bar(List<int> collection)
                    {
                        return 42;
                    }
                }

                public class PropertyTests
                {
                    public List<int> A {get; set;}
                    public HashSet<int> B {get; set;}
                    public DateTime Date {get; set;}
                }

                public struct PropertyStructTests
                {
                    public List<int> A {get; set;}
                    public HashSet<int> B {get; set;}
                    public DateTime Date {get; set;}
                }
                List<int> collection = new(); //allocate
                DateTime date = new(); //no allocation

                Dictionary<int, List<int>> field = new() {
                {1, new() { 1, 2, 3 } }
                }; //allocate 2 time

                Foo.Bar(new()); //allocate

                (int a, int b) t = new(); //does not allocate

                var toto = new PropertyTests() 
                { //The initialization expression raise one allocation no matter the number of property
                    A = new(),
                    B = new() {1, 3, 5} ,
                    Date = new()
                };

                var structTest = new PropertyStructTests() 
                { 
                    A = new(), // allocate
                    B = new() {1, 3, 5} , // allocate
                    Date = new() // does not allocate
                };
                """;
            var analyser = new ExplicitAllocationAnalyzer(true);

            var expected = analyser.GetType().GetProperty("Expressions", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(analyser) as SyntaxKind[];
            var info = ProcessCode(analyser, sampleProgram, [..expected!]);

            Assert.AreEqual(10, info.Allocations.Count);
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.TargetTypeNewRule.Id, line: 25, character: 1);
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.TargetTypeNewRule.Id, line: 28, character: 1);
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.TargetTypeNewRule.Id, line: 29, character: 5);
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.TargetTypeNewRule.Id, line: 32, character: 1);
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.NewObjectRule.Id, line: 36, character: 1);
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.InitializerCreationRule.Id, line: 37, character: 1);
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.TargetTypeNewRule.Id, line: 38, character: 9);
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.TargetTypeNewRule.Id, line: 39, character: 9);
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.TargetTypeNewRule.Id, line: 45, character: 9);
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.TargetTypeNewRule.Id, line: 46, character: 9);
        }

        [TestMethod]
        public void ExplicitAllocation_MethodReturns()
        {
            // language=csharp
            const string sampleProgram =
                """

                using System;
                using System.Collections.Generic;
                public class Foo
                {
                    public List<int> Data {get; set;}
                
                    public List<int> A(){
                        return new() {1,5,6}; //allocate
                    }
                
                    public List<int> B(){
                        return Data; //does not allocate
                    }
                
                    public (List<int> Data, int Size) C(){
                        return (new(32), 56); //allocate;
                    }
                }
                """;
            var analyser = new ExplicitAllocationAnalyzer(true);
            var expected = analyser.GetType().GetProperty("Expressions", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(analyser) as SyntaxKind[];
            var info = ProcessCode(analyser, sampleProgram, [..expected!]);
            Assert.AreEqual(2, info.Allocations.Count);
        }

        [TestMethod]
        public void ExplicitAllocation_IgnoreExceptions()
        {
            // language=csharp
            const string sampleProgram =
                """

                using System;

                throw new Exception("Agrou");
                throw new("Grou");
                throw new ArgumentException("Foo");

                """;
            var analyser = new ExplicitAllocationAnalyzer(true);
            var expected = analyser.GetType().GetProperty("Expressions", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(analyser) as SyntaxKind[];
            var info = ProcessCode(analyser, sampleProgram, [..expected!]);
            Assert.AreEqual(0, info.Allocations.Count);
        }
    }
}
