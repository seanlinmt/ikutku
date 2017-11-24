using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using clearpixels.Helpers.database;
using ikutku.Constants;

namespace ikutku.DB
{
    public interface IRepository<TEntity> : IDisposable where TEntity : class
    {

        void BulkInsert<T>(IEnumerable<T> list, int batchSize) where T : class;

        void Insert(TEntity entity);
        void InsertRange(IEnumerable<TEntity> entities);

        /// <summary>
        /// Delete an entity using its primary key.
        /// </summary>
        void Delete(object id);
        void DeleteRange(IEnumerable<TEntity> entities);
        void DeleteRange(Expression<Func<TEntity, bool>> where);

        /// <summary>
        /// Delete the given entity.
        /// </summary>
        TEntity Delete(TEntity entity);

        int DeleteAll(string matchColumnName, IEnumerable<string> ids, int batchSize = General.DB_DELETE_BATCHSIZE);
        int DeleteAll(string matchColumnName, IEnumerable<long> ids, int batchSize = General.DB_DELETE_BATCHSIZE);
        int DeleteAll(string matchColumnName, string id, int batchSize = General.DB_DELETE_BATCHSIZE);
        int DeleteAll(IEnumerable<Tuple<string, dynamic>> criterias, int batchSize = General.DB_DELETE_BATCHSIZE);

        /// <summary>
        /// Finds one entity based on provided criteria.
        /// </summary>
        TEntity FindOne(Expression<Func<TEntity, bool>> where);
        TEntity FindOneInclude(Expression<Func<TEntity, bool>> where,
                               params Expression<Func<TEntity, object>>[] includeProperties);
        TEntity FindOneNoTracking(Expression<Func<TEntity, bool>> where);

        /// <summary>
        /// Finds one entity based on its Identifier.
        /// </summary>
        TEntity FindById(object id);

        /// <summary>
        /// Finds entities based on provided criteria.
        /// </summary>
        IQueryable<TEntity> FindAll(Expression<Func<TEntity, bool>> where = null);
        IQueryable<TEntity> FindAsNoTracking(Expression<Func<TEntity, bool>> where = null);

        DbSet<TEntity> GetDbSet();

        bool ProxyCreationEnabled { get; set; }
        bool AutoDetectChangesEnabled { get; set; }
        bool LazyLoadingEnabled { get; set; }

        void SetIsolationReadUncommitted();
        void SetDeadlockPriority(DeadlockPriority priority);

        void SaveChanges(bool validateOnSave = true);
    }
}