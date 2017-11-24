using System;
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Core;
using System.Data.Entity.Infrastructure;
using clearpixels.Helpers.database;
using System.Data.Entity.Validation;
using System.Linq;
using clearpixels.Logging;
using ikutku.Constants;

namespace ikutku.DB
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly DbContext _context;

        private Hashtable _repositories;

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="detectChanges"></param>
        /// <param name="dbtimeout">command timeout in seconds</param>
        public UnitOfWork(bool detectChanges = true, int dbtimeout = General.DB_COMMAND_TIMEOUT)
            : this(new ikutkuEntities(), detectChanges, dbtimeout)
        {
            
        }

        private UnitOfWork(DbContext context, bool detectChanges, int dbtimeout)
        {
            _context = context;
            _context.Configuration.AutoDetectChangesEnabled = detectChanges;
            _context.Database.CommandTimeout = dbtimeout;
        }

        public void Dispose()
        {
            if (_context != null)
            {
                _context.Dispose();
            }

            //GC.SuppressFinalize(this);
        }

        public void SaveChanges(bool validateOnSave = true)
        {
            _context.SaveChanges(validateOnSave);
        }

        public IRepository<T> Repository<T>() where T : class
        {
            if (_repositories == null)
                _repositories = new Hashtable();

            var type = typeof(T).Name;

            if (!_repositories.ContainsKey(type))
            {
                var repositoryType = typeof(Repository<>);

                var repositoryInstance =
                    Activator.CreateInstance(repositoryType
                            .MakeGenericType(typeof(T)), _context);

                _repositories.Add(type, repositoryInstance);
            }

            return (IRepository<T>)_repositories[type];
        }

        public void SetIsolationReadUncommitted()
        {
            _context.Database.ExecuteSqlCommand("SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;");
        }

        public void SetDeadlockPriority(DeadlockPriority priority)
        {
            _context.Database.ExecuteSqlCommand(string.Format("SET DEADLOCK_PRIORITY {0};", priority.ToString()));
        }

        public int ExecuteSqlNonQuery(string cmdtext)
        {
            int rows = 0;
            using (var cmd = _context.GetNewCommand(cmdtext, General.DB_COMMAND_TIMEOUT))
            {
                try
                {
                    if (cmd.Connection.State != ConnectionState.Open)
                    {
                        cmd.Connection.Open();
                    }
                    rows = cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {

                    Syslog.Write(ex, "FAIL NonQuery => " + cmdtext);
                }
                finally
                {
                    cmd.Connection.Close();
                }
                
            }

            return rows;
        }
    }
}
