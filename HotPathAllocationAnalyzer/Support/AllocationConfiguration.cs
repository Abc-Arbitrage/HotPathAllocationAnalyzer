using System;
using System.Linq.Expressions;

namespace HotPathAllocationAnalyzer.Support
{
    public abstract class AllocationConfiguration
    {
        public void MakeSafe<T>(Expression<Func<T>> expression)
        {
        }
        
        public void MakeSafe(Expression<Action> expression)
        {
        }
    }
}
