using System;
using System.Data.Entity.Core;
using System.Data.Entity.Infrastructure;

namespace ikutku.DB
{
    // http://msdn.microsoft.com/en-us/data/dn456835
    class ikutkuDbExecutionStrategy : DbExecutionStrategy
    {
        protected override bool ShouldRetryOn(Exception exception)
        {
            if (exception is EntityException ||
                exception is DbUpdateException)
            {
                return true;
            }

            return false;
        }
    }
}
