using System;
using clearpixels.Helpers.database;

namespace ikutku.DB
{
    public interface IUnitOfWork : IDisposable
    {
        void SaveChanges(bool validateOnSave = true);
        IRepository<T> Repository<T>() where T : class;
        void SetIsolationReadUncommitted();
        void SetDeadlockPriority(DeadlockPriority priority);
        int ExecuteSqlNonQuery(string cmdtext);
        bool ProxyCreationEnabled { get; set; }
        bool AutoDetectChangesEnabled { get; set; }
        bool LazyLoadingEnabled { get; set; }
    }
}
