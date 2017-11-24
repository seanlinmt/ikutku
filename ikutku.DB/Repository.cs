using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using EntityFramework.Extensions;
using clearpixels.Helpers.database;
using clearpixels.Logging;
using ikutku.Constants;

namespace ikutku.DB
{
    public sealed class Repository<TEntity> : IRepository<TEntity> where TEntity : class
    {
        DbContext _context;

        readonly DbSet<TEntity> _dbSet;

        public bool ProxyCreationEnabled
        {
            get { return _context.Configuration.ProxyCreationEnabled; }
            set { _context.Configuration.ProxyCreationEnabled = value; }
        }

        public bool AutoDetectChangesEnabled
        {
            get { return _context.Configuration.AutoDetectChangesEnabled; }
            set { _context.Configuration.AutoDetectChangesEnabled = value; }
        }

        public bool LazyLoadingEnabled
        {
            get { return _context.Configuration.LazyLoadingEnabled; }
            set { _context.Configuration.LazyLoadingEnabled = value; }
        }

        public void SetIsolationReadUncommitted()
        {
            _context.Database.ExecuteSqlCommand("SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;");
        }

        public void SetDeadlockPriority(DeadlockPriority priority)
        {
            _context.Database.ExecuteSqlCommand(string.Format("SET DEADLOCK_PRIORITY {0};", priority.ToString()));
        }

        public Repository(int cmdTimeout = General.DB_COMMAND_TIMEOUT)
            : this(new ikutkuEntities())
        {
            _context.Database.CommandTimeout = cmdTimeout;
        } 

        public Repository(DbContext dbContext)
        {
            _context = dbContext;
#if DEBUG
            //_context.Database.Log = Console.Write;
#endif
            _dbSet = _context.Set<TEntity>();
        }

        // http://odetocode.com/blogs/scott/archive/2013/02/08/working-with-sqlbulkcopy.aspx 
        public void BulkInsert<T>(IEnumerable<T> list, int batchSize) where T : class
        {
            using (var connection = new SqlConnection(_context.Database.Connection.ConnectionString))
            {
                connection.Open();

                using (var reader = new EntityDataReader<T>(list))
                {
                    using (var sbc = new SqlBulkCopy(connection))
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string colName = reader.GetName(i);
                            sbc.ColumnMappings.Add(colName, colName);
                        }

                        sbc.BatchSize = batchSize;
                        sbc.BulkCopyTimeout = _context.Database.CommandTimeout ?? General.DB_COMMAND_TIMEOUT; // seconds
                        sbc.DestinationTableName = _context.GetTableName<T>();

                        sbc.WriteToServer(reader);
                    }
                }
            }
        }

        public void Insert(TEntity entity)
        {
            _dbSet.Add(entity);
        }

        public void InsertRange(IEnumerable<TEntity> entities)
        {
            _dbSet.AddRange(entities);
        }

        public void Delete(object id)
        {
            var item = _dbSet.Find(id);
            if (item != null)
            {
                Delete(item);
            }
        }
        public void DeleteRange(IEnumerable<TEntity> entities)
        {
            _dbSet.RemoveRange(entities);
        }

        public void DeleteRange(Expression<Func<TEntity, bool>> where)
        {
            var deleted = General.DB_DELETE_BATCHSIZE;
            while (deleted == General.DB_DELETE_BATCHSIZE)
            {
                deleted = _dbSet.Where(where).Take(General.DB_DELETE_BATCHSIZE).Delete();
            }
        }

        public TEntity Delete(TEntity entity)
        {
            return _dbSet.Remove(entity);
        }

        public int DeleteAll(string matchColumnName, IEnumerable<string> ids, int batchSize = General.DB_DELETE_BATCHSIZE)
        {
            if (!ids.Any())
            {
                return 0;
            }

            return DeleteAll(new[] { new Tuple<string, dynamic>(matchColumnName, ids) }, batchSize);
        }

        public int DeleteAll(string matchColumnName, IEnumerable<long> ids, int batchSize = General.DB_DELETE_BATCHSIZE)
        {
            if (!ids.Any())
            {
                return 0;
            }

            return DeleteAll(new[] { new Tuple<string, dynamic>(matchColumnName, ids) }, batchSize);
        }

        public int DeleteAll(string matchColumnName, string id, int batchSize = General.DB_DELETE_BATCHSIZE)
        {
            return DeleteAll(new[] { new Tuple<string, dynamic>(matchColumnName, id) }, batchSize);
        }


        // http://www.dirigodev.com/blog/web-development-execution/deleting-large-amounts-of-rows-from-sql-2005-2008-database-tables/
        public int DeleteAll(IEnumerable<Tuple<string, dynamic>> criterias, int batchSize = General.DB_DELETE_BATCHSIZE)
        {
            var matches = new List<string>();
            var numberOfListConditions = 0;

            Tuple<string, dynamic> listmatch = null;

            foreach (var criteria in criterias)
            {
                if (criteria.Item2 as IEnumerable<string> != null)
                {
                    var list = ((IEnumerable<string>)criteria.Item2);
                    if (list.Any())
                    {

                        if (numberOfListConditions++ == 2)
                        {
                            throw new NotImplementedException("unsupported match condition");
                        }

                        listmatch = criteria;
                    }

                }
                else if (criteria.Item2 as IEnumerable<long> != null)
                {
                    var list = ((IEnumerable<long>)criteria.Item2);
                    if (list.Count() != 0)
                    {

                        if (numberOfListConditions++ == 2)
                        {
                            throw new NotImplementedException("unsupported match condition");
                        }

                        listmatch = criteria;
                    }

                }
                else if (criteria.Item2 is string)
                {
                    matches.Add(string.Format("{0}='{1}'", criteria.Item1, criteria.Item2));
                }
                else
                {
                    matches.Add(string.Format("{0}={1}", criteria.Item1, criteria.Item2));
                }
            }

            if (listmatch == null)
            {
                var selectcmd = string.Format("select top({0}) * from {1} where {2}", batchSize, _context.GetTableName<TEntity>(), string.Join(" AND ", matches));

                int total = 0;
                var round = 0;

                while (true)
                {
                    var viewname = string.Format("view_{0}_{1}", round++, DateTime.UtcNow.Ticks);
                    var createviewcmd = string.Format("create view {0} as ({1})", viewname, selectcmd);
                    var deletecmd = string.Format("set deadlock_priority low;delete from {0};", viewname);
                    var deleteviewcmd = string.Format("drop view {0}", viewname);

                    ExecuteDbCommmand(createviewcmd);
                    int rows = ExecuteDbCommmand(deletecmd, selectcmd);
                    ExecuteDbCommmand(deleteviewcmd);

                    total += rows;
                    if (rows < batchSize)
                    {
                        break;
                    }
                }
                return total;
            }
            else
            {
                IOrderedEnumerable<ulong> ids;

                if (listmatch.Item2 as IList<string> == null)
                {
                    ids = ((IList<long>)listmatch.Item2).Select(Convert.ToUInt64).OrderBy(x => x);
                }
                else
                {
                    ids = ((IList<string>)listmatch.Item2).Select(x => Convert.ToUInt64(x)).OrderBy(x => x);
                }

                int round = 0;
                int total = 0;
                var tablename = _context.GetTableName<TEntity>();
                while (true)
                {
                    var viewname = string.Format("view_{0}_{1}", round, DateTime.UtcNow.Ticks);

                    var working = ids.Skip(round++ * batchSize).Take(batchSize).ToArray();

                    if (working.Length == 0)
                    {
                        break;
                    }

                    string matchstring;
                    if (listmatch.Item2 as IList<string> == null)
                    {
                        matchstring = string.Format("{0} in ({1})", listmatch.Item1,
                                                    string.Join(", ", working));
                    }
                    else
                    {
                        matchstring = string.Format("{0} in ({1})", listmatch.Item1,
                                                    string.Join(", ", working.Select(
                                                        x => string.Format("'{0}'", x))));
                    }

                    var selectcmd = string.Format("select top({0}) * from {1} where {2}", batchSize,
                                              tablename,
                                              string.Join(" AND ", matches.Union(new[] { matchstring })));

                    var deletecmd = string.Format("set deadlock_priority low;delete from {0};", viewname);
                    var deleteviewcmd = string.Format("drop view {0}", viewname);

                    var createviewcmd = string.Format("create view {0} as ({1})", viewname, selectcmd);

                    ExecuteDbCommmand(createviewcmd);
                    total += ExecuteDbCommmand(deletecmd, selectcmd);
                    ExecuteDbCommmand(deleteviewcmd);

                    if (working.Length < batchSize)
                    {
                        break;
                    }
                }
                return total;
            }
        }

        private int ExecuteDbCommmand(string cmdstring, string contextcmd = "", int retryCount = 0)
        {
            int rows = 0;
            using (var cmd = _context.GetNewCommand(cmdstring, General.DB_COMMAND_TIMEOUT))
            {
                try
                {
                    if (cmd.Connection.State != ConnectionState.Open)
                    {
                        cmd.Connection.Open();
                    }
                    rows = cmd.ExecuteNonQuery();

                }
                catch (SqlException ex)
                {
                    if (retryCount < 10)
                    {
                        System.Threading.Thread.Sleep(500);
                        ExecuteDbCommmand(cmdstring, contextcmd, ++retryCount);
                    }
                    else
                    {
                        Syslog.Write(ex, "FAIL DeleteQuery => " + (string.IsNullOrEmpty(contextcmd)?cmdstring:contextcmd));
                    }
                }
                catch (Exception ex)
                {
                    Syslog.Write(ex, "FAIL DeleteQuery => " + (string.IsNullOrEmpty(contextcmd) ? cmdstring : contextcmd));
                }
                finally
                {
                    cmd.Connection.Close();
                    Debug.Write(string.Format("{0} rows <= {1}", rows, cmdstring));
                }
            }
            
            return rows;
        }

        public TEntity FindById(object id)
        {
            return _dbSet.Find(id);
        }

        public TEntity FindOne(Expression<Func<TEntity, bool>> where)
        {
            return FindAll(where).SingleOrDefault();
        }

        public TEntity FindOneInclude(Expression<Func<TEntity, bool>> where, params Expression<Func<TEntity, object>>[] includeProperties)
        {
            foreach (var property in includeProperties)
            {
                _dbSet.Include(property);
            }
            return _dbSet.Where(where).SingleOrDefault();
        }

        public TEntity FindOneNoTracking(Expression<Func<TEntity, bool>> where)
        {
            return FindAsNoTracking(where).SingleOrDefault();
        }

        public IQueryable<TEntity> FindAll(Expression<Func<TEntity, bool>> where = null)
        {
            return null != where ? _dbSet.Where(where) : _dbSet;
        }

        public IQueryable<TEntity> FindAsNoTracking(Expression<Func<TEntity, bool>> where = null)
        {
            return null != where ? _dbSet.AsNoTracking().Where(where) : _dbSet.AsNoTracking();
        }

        public DbSet<TEntity> GetDbSet()
        {
            return _dbSet;
        }

        public void SaveChanges(bool validateOnSave = true)
        {
            _context.SaveChanges(validateOnSave);
        }

        public void Dispose()
        {
            if (_context != null)
            {
                _context.Dispose();
                _context = null;
            }
        }

        internal IQueryable<TEntity> Get(
            Expression<Func<TEntity, bool>> filter = null,
            Func<IQueryable<TEntity>,
                IOrderedQueryable<TEntity>> orderBy = null,
            List<Expression<Func<TEntity, object>>>
                includeProperties = null,
            int? page = null,
            int? pageSize = null)
        {
            IQueryable<TEntity> query = _dbSet;

            if (includeProperties != null)
                includeProperties.ForEach(i => { query = query.Include(i); });

            if (filter != null)
                query = query.Where(filter);

            if (orderBy != null)
                query = orderBy(query);

            if (page != null && pageSize != null)
                query = query
                    .Skip((page.Value - 1) * pageSize.Value)
                    .Take(pageSize.Value);

            return query;
        }
    }
}