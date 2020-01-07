using System.Collections.Generic;

namespace ClrHeapAllocationAnalyzer.Test
{
    public class TestConfiguration : AllocationConfiguration
    {
        public void WhitelistString(string str)
        {
            MakeSafe(() => str.IsNormalized());
            MakeSafe(() => str.Contains(default));
        }

        public void WhitelistList<T>(List<T> list)
        {
            MakeSafe(() => list.Clear());
        }
    }
}
