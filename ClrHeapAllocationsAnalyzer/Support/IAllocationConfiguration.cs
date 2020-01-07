using System;
using System.Linq.Expressions;

namespace ClrHeapAllocationAnalyzer
{
    public abstract class IAllocationConfiguration
    {
        protected IAllocationConfiguration()
        {
        }
        
        protected void MakeSafe(Expression<Action> expression)
        {
            throw new NotImplementedException();
        }
    }
}
