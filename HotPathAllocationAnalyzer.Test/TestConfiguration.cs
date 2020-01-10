using HotPathAllocationAnalyzer.Support;

namespace HotPathAllocationAnalyzer.Test
{
    public class TestConfiguration : AllocationConfiguration
    {
        public void WhitelistString(string str)
        {
            MakeSafe(() => str.Length);
            MakeSafe(() => str.IsNormalized());
            MakeSafe(() => str.Contains(default));
        }

        public void WhitelistNullable<T>(T? arg)
            where T: struct
        {
            MakeSafe(() => arg.Value); 
            MakeSafe(() => arg.HasValue); 
        }
    }
}
