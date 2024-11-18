using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace HotPathAllocationAnalyzer.Analyzers
{
    internal static class SyntaxHelper
    {
        public static T? FindContainer<T>(this SyntaxNode tokenParent) where T : SyntaxNode
        {
            if (tokenParent is T invocation)
            {
                return invocation;
            }

            return tokenParent.Parent == null ? null : FindContainer<T>(tokenParent.Parent);
        }

        public static (Type type, SyntaxNode? ancestor) FindAncestor(this SyntaxNode node, params SyntaxKind[] researchedKind)
        {
            var current = node;
            while (current.Parent != null)
            {
                if (researchedKind.Contains(current.Kind()))
                {
                    return (current.GetType(), current);
                }

                current = current.Parent;
            }
            return new(typeof(object), null);
        }

        public static SyntaxNode? SearchPath(this SyntaxNode node, params SyntaxKind[] path)
        {
            var current = node;
            int index = 0;
            while (index < path.Length)
            {
                current = current.Parent;
                if (current == null)
                    return null;
                if (!current.IsKind(path[index]))
                    return null;
                index++;
            }
            return current;
        }
    }
}