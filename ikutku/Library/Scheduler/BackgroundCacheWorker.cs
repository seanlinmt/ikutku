using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using clearpixels.Helpers.database;
using clearpixels.Logging;
using ikutku.Constants;
using ikutku.DB;
using ikutku.Library.Workers;

namespace ikutku.Library.Scheduler
{
    // with current API limit we can refresh at 576000/day + 17280/day = 593280/day
    // only refreshes. never deletes
    public sealed class BackgroundCacheWorker
    {
        public readonly static BackgroundCacheWorker Instance = new BackgroundCacheWorker();

        private BackgroundCacheWorker()
        {
            
        }

        // 30 x 10 = 300 secs = 5 mins
        public void DeleteStaleCacheEveryFiveMinutes()
        {
            try
            {
                using (var unitOfWork = new UnitOfWork(false, 30))
                {
                    unitOfWork.SetDeadlockPriority(DeadlockPriority.LOW);

                    // because this gets triggered before FindAndDeleteStaleAccount so we get errors and need to do this the day after
                    // also in case user signs in again
                    var expiryDate = DateTime.UtcNow.AddDays(-(General.DB_ACCOUNT_VALID_DAYS + 1));

                    for (int i = 0; i < 10; i++)
                    {
                        unitOfWork.ExecuteSqlNonQuery(string.Format("delete top(1000) from cachedUsers where updated < '{0}'", expiryDate));
                    }
                }
            }
#if DEBUG
            catch (ThreadAbortException)
            {

            }
#endif
            catch (Exception ex)
            {
                Syslog.Write(ex);
            }
        }
        
        public void Start()
        {
#if DEBUG
            var stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
            try
            {
                using (var cachedUserRepository = new Repository<cachedUser>(30))
                {
                    // take up to 60 / 15 x 100 entries. But less 100 so that rate limit is not hit
                    var twitterWorker = TwitterWorker.GetIkutkuWorker(true);
                    var worker = new DatabaseWorker(twitterWorker);
                    for (int i = 0; i < 3; i++)
                    {
                        var twitterIDs = cachedUserRepository
                                     .FindAll()
                                     .OrderBy(x => x.updated)
                                     .Take(100)
                                     .Select(x => x.twitterid)
                                     .ToArray();

                        worker.UpdateProfiles(twitterIDs, DeadlockPriority.LOW);
                    }
                }
            }
#if DEBUG
            catch (ThreadAbortException)
            {

            }
#endif
            catch (Exception ex)
            {
                Syslog.Write(ex);
            }
#if DEBUG
            stopwatch.Stop();
            if (stopwatch.Elapsed.Seconds > 60)
            {
                Syslog.Write("DoWorkViaApi took {0} seconds", stopwatch.Elapsed.Seconds);
            }
#endif
        }
    }
}