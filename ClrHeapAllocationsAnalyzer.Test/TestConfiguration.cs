using System.Collections.Generic;
using ClrHeapAllocationAnalyzer.Support;

namespace ClrHeapAllocationAnalyzer.Test
{
    public class TestConfiguration : AllocationConfiguration
    {
        public void WhitelistString(string str)
        {
            MakeSafe(() => str.Length);
            MakeSafe(() => str.IsNormalized());
            MakeSafe(() => str.Contains(default));
        }

        public void WhitelistList<T>(List<T> list)
        {
            MakeSafe(() => list.Clear());
        }

        public void WhitelistNullable<T>(T? arg)
            where T: struct
        {
            MakeSafe(() => arg.Value); 
        }
    }
}
