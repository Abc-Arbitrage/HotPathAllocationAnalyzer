using System;
using System.Linq.Expressions;

namespace ClrHeapAllocationAnalyzer
{
    public abstract class AllocationConfiguration
    {
        public void MakeSafe(Expression<Action> expression)
        {
        }
    }
}
