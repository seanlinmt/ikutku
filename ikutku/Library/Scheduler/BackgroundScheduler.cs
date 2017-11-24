
/*
 * LICENSE NOTE:
 *
 * Copyright  2012-2013 Clear Pixels Limited, All Rights Reserved.
 *
 * Unless explicitly acquired and licensed from Licensor under another license, the
 * contents of this file are subject to the Reciprocal Public License ("RPL")
 * Version 1.5, or subsequent versions as allowed by the RPL, and You may not copy
 * or use this file in either source code or executable form, except in compliance
 * with the terms and conditions of the RPL. 
 *
 * All software distributed under the RPL is provided strictly on an "AS IS" basis,
 * WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESS OR IMPLIED, AND LICENSOR HEREBY
 * DISCLAIMS ALL SUCH WARRANTIES, INCLUDING WITHOUT LIMITATION, ANY WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, QUIET ENJOYMENT, OR
 * NON-INFRINGEMENT. See the RPL for specific language governing rights and
 * limitations under the RPL.
 *
 * @author         Sean Lin Meng Teck <seanlinmt@clearpixels.co.nz>
 * @copyright      2012-2013 Clear Pixels Limited
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using clearpixels.Helpers.datetime;
using clearpixels.Helpers.datetime.timer;
using clearpixels.Logging;
using ikutku.Constants;
using ikutku.DB;
using ikutku.DB.extension;
using ikutku.Library.Workers;
using ikutku.Models.queue;
using ikutku.Models.user;
using user = ikutku.DB.user;

namespace ikutku.Library.Scheduler
{
    public sealed class BackgroundScheduler
    {
        public readonly static BackgroundScheduler Instance = new BackgroundScheduler();
        private bool _processQueuedFollowingUsersRunning;

        private const byte MAX_QUEUE_LENGTH = 2;

        private readonly ConcurrentDictionary<string, string> _usersInProgress;

        private BackgroundScheduler()
        {
            _usersInProgress = new ConcurrentDictionary<string, string>();
        }

        public void ProcessQueuedUsers()
        {
            var inprogressIDs = _usersInProgress.Keys.ToArray();
            var timenow = DateTime.UtcNow.ToUnixTime();

            // exclude users in timer queue
            var waitqueued = GetWaitingWorkerIDs();
            QueueInfo[] queueInfos;

            using (var repository = new Repository<queuedUser>())
            {
                repository.AutoDetectChangesEnabled = false;
                var query = repository
                               .FindAsNoTracking(x =>
                                    x.user.oauthSecret != null &&
                                    (x.user.apiNextRetry == null || x.user.apiNextRetry < timenow) &&
                                        !waitqueued.Contains(x.ownerid) &&
                                        !inprogressIDs.Contains(x.ownerid))
                               .OrderBy(x => x.id);

                queueInfos = query
                    .ToModel()
                    .ToArray();
            }


            for (int i = 0; i < queueInfos.Length; i++)
            {
                QueueInfo q1 = queueInfos[i];
                if (_usersInProgress.TryAdd(q1.auth.twitterUserid, q1.auth.twitterUserid))
                {
                    new Thread(() => HandleDiffRebuild(q1)).Start();
                }
            }
        }

        private void HandleDiffRebuild(QueueInfo q)
        {
            Thread.CurrentPrincipal = new GenericPrincipal(new GenericIdentity("SYS_" + q.auth.twitterUsername),
                                                            null);

            var builder = new DatabaseWorker(q.auth, q.settings);

            try
            {
                if (q.reset || q.settings.HasFlag(QueueSettings.RESET))
                {
                    builder.ResetRun();
                }
                builder.StartDiffRebuild();
            }
            catch (Exception ex)
            {
                Syslog.Write(ex);
                builder.RemoveFromDiffQueue(true);
            }
            finally
            {
                string temp;
                if (!_usersInProgress.TryRemove(q.auth.twitterUserid, out temp))
                {
                    Syslog.Write("InProgressRemove FAIL {0}", q.auth.twitterUserid);
                }

                ProcessQueuedUsers();
            }
        }

        public void ProcessQueuedFollowingUsers()
        {
            if (_processQueuedFollowingUsersRunning)
            {
                return;
            }

            _processQueuedFollowingUsersRunning = true;

            // exclude users in timer queue
            var waitqueued = GetWaitingWorkerIDs();
            var timenow = DateTime.UtcNow.ToUnixTime();

            AuthInfo[] authInfos;

            using (var repository = new Repository<queuedFollowingUser>())
            {
                repository.AutoDetectChangesEnabled = false;
                var query = repository
                               .FindAsNoTracking(x =>
                                        !waitqueued.Contains(x.ownerid) && 
                                        x.user.oauthSecret != null &&
                                        (x.user.apiNextRetry == null || x.user.apiNextRetry < timenow))
                               .OrderBy(x => x.id);

                authInfos = query
                    .ToModel()
                    .ToArray();
            }

            foreach (var q in authInfos)
            {
                Thread.CurrentPrincipal = new GenericPrincipal(new GenericIdentity("SYS_" + q.twitterUsername),
                                                            null);

                var builder = new DatabaseWorker(q);

                try
                {
                    builder.StartFollowingsRebuild();
                }
                catch (Exception ex)
                {
                    Syslog.Write(ex);
                    builder.RemoveFromFollowingsQueue(true);
                }
            }

            _processQueuedFollowingUsersRunning = false;
        }

        // removes account that has not been logged into for DBACCOUNT_VALID_DAYS
        public void FindAndDeleteStaleAccount()
        {
            var staleDate = DateTime.UtcNow.AddDays(-General.DB_ACCOUNT_VALID_DAYS);
            AuthInfo[] authInfos;

            using (var unitOfWork = new UnitOfWork(false))
            {
                authInfos = unitOfWork.Repository<user>()
                    .FindAsNoTracking(x => x.lastLogin < staleDate)
                    .ToAuthInfo().ToArray();
            }

            if (authInfos.Any())
            {
                foreach (var auth in authInfos)
                {
                    Thread.CurrentPrincipal = new GenericPrincipal(new GenericIdentity("SYS_" + auth.twitterUsername),
                                                            null);
                    var worker = new DatabaseWorker(auth);
                    try
                    {
                        worker.PurgeUser(StringsResource.ACC_RESET_STALE_MESSAGE);
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
            }
        }

        public string[] GetWaitingWorkerIDs()
        {
            var ids = TimerWaitQueue.Instance.GetAllKeys(General.IKUTKU_SCREENNAME)
                                                .Select(x => x.Split(new[] { '_' }).Last())
                                                .ToArray();
            return ids;
        }

        public string[] GetInProgressWorkerIDs()
        {
            return _usersInProgress.Keys.ToArray();
        }

        public IEnumerable<string> GetWaitingWorkers()
        {
             return TimerWaitQueue.Instance.GetAllTimers().Select(x => string.Format("{0}:{1}", x.Info.Key, x.Info.Name));
        }
    }
}
