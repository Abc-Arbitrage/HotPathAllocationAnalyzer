using HotPathAllocationAnalyzer.Support;

#pragma warning disable CS8629 // Nullable value type may be null.
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

namespace HotPathAllocationAnalyzer.Test
{
    public class TestConfiguration : AllocationConfiguration
    {
        public void WhitelistString(string str)
        {
            MakeSafe(() => str.Length);
            MakeSafe(() => str.IsNormalized());
            MakeSafe(() => str.Contains(default(string)));
        }

        public void WhitelistNullable<T>(T? arg)
            where T: struct
        {
            MakeSafe(() => arg.Value); 
            MakeSafe(() => arg.HasValue); 
        }
    }
}
