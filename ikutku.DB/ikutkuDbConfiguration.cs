using System.Data.Entity;
using System.Data.Entity.Infrastructure;

namespace ikutku.DB
{
    class ikutkuDbConfiguration : DbConfiguration
    {
        public ikutkuDbConfiguration()
        {
            SetExecutionStrategy(
            "System.Data.SqlClient",
            () => new ikutkuDbExecutionStrategy());
        }
    }
}
