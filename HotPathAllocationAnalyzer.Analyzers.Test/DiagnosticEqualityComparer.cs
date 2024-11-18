using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace HotPathAllocationAnalyzer.Test
{
    internal class DiagnosticEqualityComparer : IEqualityComparer<Diagnostic>
    {
        public static readonly DiagnosticEqualityComparer Instance = new();

        public bool Equals(Diagnostic? x, Diagnostic? y)
        {
            return object.Equals(x, y);
        }

        public int GetHashCode(Diagnostic? obj)
        {
            return Combine(obj?.Descriptor.GetHashCode(),
                        Combine(obj?.GetMessage().GetHashCode(),
                         Combine(obj?.Location.GetHashCode(),
                          Combine(obj?.Severity.GetHashCode(), obj?.WarningLevel)
                        )));
        }

        private static int Combine(int? newKeyPart, int? currentKey)
        {
            var hash = unchecked(currentKey.GetValueOrDefault() * (int)0xA5555529);

            if (newKeyPart.HasValue)
            {
                return unchecked(hash + newKeyPart.Value);
            }

            return hash;
        }
    }
}
