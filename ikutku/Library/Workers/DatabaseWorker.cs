using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using LinqToTwitter;
using clearpixels.Helpers.concurrency;
using clearpixels.Helpers.database;
using clearpixels.Helpers.datetime.timer;
using clearpixels.Models;
using ikutku.DB.extension;
using clearpixels.Helpers;
using clearpixels.Helpers.datetime;
using clearpixels.Logging;
using ikutku.Constants;
using ikutku.DB;
using ikutku.Library.Workers.Helpers;
using ikutku.Library.Workers.Models;
using ikutku.Models.queue;
using ikutku.Models.twitter;
using ikutku.Models.user;
using ikutku.Models.user.followers;
using Settings = ikutku.Models.user.Settings;
using User = ikutku.Models.user.User;

namespace ikutku.Library.Workers
{
    public class DatabaseWorker
    {
        private const int CACHERESOLVER_WORKINGSIZE = 1500; // limit is 1200 but we add extra to trigger api limit

        private readonly AuthInfo _auth;

        private readonly TwitterWorker _twitterWorker;
        public bool IsFollowingIkutku { get { return _twitterWorker.IsFollowing(General.IKUTKU_USERID); } }

        private QueueSettings _rebuildSettings;
        private string _purgeReason;
        private TimerQueueIntent _queueIntent;

        private bool _userQueued { get { return _rebuildSettings.HasFlag(QueueSettings.USER_TRIGGERED); } }

        public DatabaseWorker(AuthInfo authInfo, QueueSettings settings = QueueSettings.NONE)
            : this(new TwitterWorker(authInfo))
        {
            _auth = authInfo;
            _rebuildSettings = settings;
        }

        public DatabaseWorker(TwitterWorker worker)
        {
            _twitterWorker = worker;
            _purgeReason = null;
            _queueIntent = null;
        }

        private void IncrementAuthFailureCounter(TwitterErrorCode errorCode, bool dontQueue = false, int count = 1)
        {
            int failed;
            using (var usrRepository = new Repository<user>())
            {
                var usr = usrRepository.FindById(_auth.twitterUserid);
                if (usr == null)
                {
                    return;
                }
                usr.authFailCount += count;
                failed = usr.authFailCount;
                usrRepository.SaveChanges(false);
            }

            if (failed > General.AUTH_MAX_FAILURES)
            {
                _purgeReason = StringsResource.ACC_RESET_AUTH_MESSAGE;
            }
            else
            {
                if (!dontQueue)
                {
                    _queueIntent = new TimerQueueIntent(errorCode.ToString(), 10);
                }
            }
        }

        private static void ErrorIncrementCounter(int errorCode)
        {
            using (var errCounterRepository = new Repository<errorCounter>())
            {
                var single = errCounterRepository.FindOne(x => x.type == errorCode);
                if (single == null)
                {
                    errCounterRepository.Insert(new errorCounter(){ type = errorCode, count = 1});
                }
                else
                {
                    single.count++;
                }
                errCounterRepository.SaveChanges(false);
            }
        }

        public QueueSettings IsAccountUpToDate(bool breakOnFirstFailure = false)
        {
            var queueSettings = QueueSettings.NONE;
            int? followerCount;
            int? followingCount;
            int? userListCount;
            usersList[] dbLists;

            using (var userRepository = new Repository<user>())
            {
                userRepository.AutoDetectChangesEnabled = false;
                var usr = userRepository.FindOneInclude(x => x.id == _auth.twitterUserid, x => x.usersLists);
                followerCount = usr.followerCountTotal;
                followingCount = usr.followingCountTotal;
                userListCount = usr.userlistCount;
                dbLists = usr.usersLists.ToArray();
            }

            try
            {
                var identity = _twitterWorker.VerifyCredentials();

                if (followerCount != identity.User.FollowersCount)
                {
                    queueSettings |= QueueSettings.BUILD_FOLLOWERS;
                    if (breakOnFirstFailure)
                    {
                        return queueSettings;
                    }
                }

                if (followingCount != identity.User.FriendsCount)
                {
                    queueSettings |= QueueSettings.BUILD_FOLLOWINGS;
                    if (breakOnFirstFailure)
                    {
                        return queueSettings;
                    }
                }

                // all lists
                if (!userListCount.HasValue ||
                    dbLists.Any(x => !x.listCursor.HasValue || x.listCursor.Value != 0))
                {
                    queueSettings |= QueueSettings.BUILD_LISTS;
                    if (breakOnFirstFailure)
                    {
                        return queueSettings;
                    }
                }
                else
                {
                    var lists = _twitterWorker.GetLists();
                    if (lists.Count != userListCount)
                    {
                        queueSettings |= QueueSettings.BUILD_LISTS;
                        if (breakOnFirstFailure)
                        {
                            return queueSettings;
                        }
                    }
                    else
                    {
                        foreach (var row in lists)
                        {
                            var list = row;
                            var dblist = dbLists.SingleOrDefault(x => x.id == list.ListIDResult);
                            if (dblist == null ||
                                list.MemberCount != dblist.memberCount ||
                                list.Name != dblist.listname)
                            {
                                queueSettings |= QueueSettings.BUILD_LISTS;
                                if (breakOnFirstFailure)
                                {
                                    return queueSettings;
                                }
                                break;
                            }
                        }
                    }
                }
            }
            catch (TwitterQueryException ex)
            {
                var errorcode = ex.ErrorCode.ToEnum<TwitterErrorCode>();

                switch (errorcode)
                {
                    case TwitterErrorCode.NO_ERROR:
                    case TwitterErrorCode.OVERCAPACITY:
                    //case TwitterErrorCode.NO_REPLY:
                    case TwitterErrorCode.RATE_LIMIT_EXCEEDED:
                        break;
                    case TwitterErrorCode.INVALID_OR_EXPIRED_CREDENTIALS:
                    case TwitterErrorCode.PAGE_DOES_NOT_EXIST:
                    case TwitterErrorCode.FAIL_AUTHENTICATION:
                        IncrementAuthFailureCounter(errorcode);
                        break;
                    case TwitterErrorCode.USER_SUSPENDED:
                    case TwitterErrorCode.ACCOUNT_SUSPENDED:
                    case TwitterErrorCode.ACCESS_DENIED:
                        _purgeReason = StringsResource.ACC_RESET_AUTH_MESSAGE;
                        break;
                    default:
                        Syslog.Write("{0} :IsAccountUpToDate Error => {1} {2}",
                            _auth.twitterUsername,
                            ex.ErrorCode,
                            ex.Message);
                        break;
                }

            }
            catch (Exception ex)
            {
                if (!(ex is ThreadAbortException))
                {
                    Syslog.Write(ex, "{0} IsAccountUpToDate Exception", _auth.twitterUsername);
                }
            }

            return queueSettings;
        }

        /// <summary>
        ///  This will also set user triggered flag for notification
        /// </summary>
        /// <returns></returns>
        public bool IsDiffDatabaseReady()
        {
            using (var qrepository = new Repository<queuedUser>())
            {
                qrepository.SetIsolationReadUncommitted();
                var q = qrepository.FindOne(x => x.ownerid == _auth.twitterUserid);

                if (q != null)
                {
                    // update to user triggered as the user now knows we're processing their account
                    q.settings |= (int)QueueSettings.USER_TRIGGERED;
                    qrepository.SaveChanges();
                    return false;
                }
            }

            return true;
        }

        public bool IsAllFollowingsDatabaseReady()
        {
            using (var qrepository = new Repository<queuedFollowingUser>())
            {
                return !qrepository.FindAsNoTracking().Any(x => x.ownerid == _auth.twitterUserid);
            }
        }

        public User[] ListUserDIffv2(FollowersListingType type, int pageno, int row, OrderByType method, string dir, bool hide_excluded, out int total)
        {
            var usrs = new List<User>();
            var expiryDays = DateTime.UtcNow.AddDays(-General.DB_ACCOUNT_VALID_DAYS);

            using (var unitOfWork = new UnitOfWork(false))
            {
                unitOfWork.SetIsolationReadUncommitted();
                IQueryable<cachedUser> cacheusrs;

                switch (type)
                {
                    case FollowersListingType.MENOFOLLOW:
                        {
                            var followings = unitOfWork.Repository<following>().FindAll(x => x.ownerid == _auth.twitterUserid).Select(x => x.twitterid);
                            cacheusrs = unitOfWork.Repository<follower>()
                                                  .FindAll(x => x.ownerid == _auth.twitterUserid &&
                                                                !followings.Contains(x.twitterid) &&
                                                                x.cachedUser.updated >= expiryDays)
                                                  .Select(x => x.cachedUser);
                        }
                        break;
                    case FollowersListingType.NOFOLLOWME:
                        {
                            var followers = unitOfWork.Repository<follower>().FindAll(x => x.ownerid == _auth.twitterUserid).Select(x => x.twitterid);
                            cacheusrs = unitOfWork.Repository<following>()
                                                  .FindAll(x => x.ownerid == _auth.twitterUserid &&
                                                                !followers.Contains(x.twitterid) &&
                                                                x.cachedUser.updated >= expiryDays)
                                                  .Select(x => x.cachedUser);
                        }
                        break;
                    case FollowersListingType.ALLFOLLOWINGS:
                        {
                            cacheusrs = unitOfWork.Repository<following>()
                                                  .FindAll(
                                                      x =>
                                                      x.ownerid == _auth.twitterUserid &&
                                                      x.cachedUser.updated >= expiryDays)
                                                  .Select(x => x.cachedUser);
                        }
                        break;
                    case FollowersListingType.ALLFOLLOWERS:
                        throw new NotImplementedException();
                    default:
                        throw new ArgumentOutOfRangeException("type");
                }

                var nullcount = cacheusrs.Count(x => x == null);
                if (nullcount != 0)
                {
                    cacheusrs = cacheusrs.Where(x => x != null);
                    Syslog.Write("ListUserDIffv2 NULL {0} {1}", nullcount, _auth.twitterUsername);
                }

                var query = cacheusrs;

                Debug.WriteLine(query);

                switch (method)
                {
                    case OrderByType.FOLLOW_DATE:
                        if (dir == "asc")
                        {
                            query = query.OrderByDescending(x => x.id);
                        }
                        else
                        {
                            query = query.OrderBy(x => x.id);
                        }
                        break;
                    case OrderByType.FOLLOWER_RATIO:
                        if (dir == "asc")
                        {
                            query = query.OrderBy(x => x.ratio);
                        }
                        else
                        {
                            query = query.OrderByDescending(x => x.ratio);
                        }
                        break;
                    case OrderByType.ACTIVITY_DATE:
                        if (dir == "asc")
                        {
                            query = query.OrderBy(x => x.lastTweet);
                        }
                        else
                        {
                            query = query.OrderByDescending(x => x.lastTweet);
                        }
                        break;
                    case OrderByType.FOLLOWING_COUNT:
                        if (dir == "asc")
                        {
                            query = query.OrderBy(x => x.followingsCount);
                        }
                        else
                        {
                            query = query.OrderByDescending(x => x.followingsCount);
                        }
                        break;
                    case OrderByType.FOLLOWER_COUNT:
                        if (dir == "asc")
                        {
                            query = query.OrderBy(x => x.followersCount);
                        }
                        else
                        {
                            query = query.OrderByDescending(x => x.followersCount);
                        }
                        break;
                    default:
                        throw new NotImplementedException();
                }

                total = query.Count();

                query = query.Skip(row * pageno).Take(row);

                var excluded = unitOfWork.Repository<usersInList>().FindAll(x => x.ownerid == _auth.twitterUserid &&
                                                                  x.usersList.exclude)
                                                            .Select(x => x.twitterid);
                if (hide_excluded)
                {
                    query = query.Where(x => !excluded.Contains(x.twitterid));
                    return query.ToModel().ToArray();
                }


                foreach (var q in query)
                {
                    var u = q.ToModel();
                    u.excluded = excluded.Contains(u.twitterUserid);
                    usrs.Add(u);
                }
            }

            return usrs.ToArray();
        }

        private bool IsSyncComplete()
        {
            bool completed = true;
            long? followersCursor;
            long? followingCursor;
            int? userListCount;
            long?[] listCursors;
            int? uncachedCount;

            using (var unitOfWork = new UnitOfWork(false))
            {
                var usr = unitOfWork.Repository<user>().FindById(_auth.twitterUserid);

                followersCursor = usr.followersCursor;
                followingCursor = usr.followingsCursor;
                userListCount = usr.userlistCount;
                listCursors = usr.usersLists.Select(x => x.listCursor).ToArray();
                uncachedCount = usr.uncachedCount;
            }

            if (!followersCursor.HasValue || followersCursor.Value != 0)
            {
                _rebuildSettings |= QueueSettings.BUILD_FOLLOWERS;
                completed = false;
            }

            if (!followingCursor.HasValue || followingCursor.Value != 0)
            {
                _rebuildSettings |= QueueSettings.BUILD_FOLLOWINGS;
                completed = false;
            }

            if (!userListCount.HasValue ||
                listCursors.Any(x => !x.HasValue || x.Value != 0))
            {
                _rebuildSettings |= QueueSettings.BUILD_LISTS;
                completed = false;
            }

            if (!uncachedCount.HasValue || uncachedCount.Value != 0)
            {
                completed = false;
            }

            return completed;
        }

        private void NotifyUserThatAccountHasBeenPurged(string reasonForPurge)
        {
            try
            {
                var ikutkuWorker = TwitterWorker.GetIkutkuWorker(false);
                ikutkuWorker.SendDirectMessage(_auth.twitterUserid, reasonForPurge);
            }
            catch (TwitterQueryException ex)
            {
                var errorcode = ex.ErrorCode.ToEnum<TwitterErrorCode>();
                switch (errorcode)
                {
                    case TwitterErrorCode.DM_FAIL_NOT_FOLLOWING:
                        break;
                    default:
                        Syslog.Write("Failed NotifyUserThatAccountHasBeenPurged {0}. {1} {2}", _auth.twitterUsername, ex.ErrorCode, ex.Message);
                        break;
                }
            }
            catch (Exception ex)
            {
                Syslog.Write(ex);
            }
        }

        private void NotifyUserThatSyncIsComplete()
        {
            if (_auth.settings.HasFlag(Settings.NO_DIRECT_MSG))
            {
                return;
            }

            try
            {
                var ikutkuWorker = TwitterWorker.GetIkutkuWorker(false);

                if (_twitterWorker.IsFollowing(General.IKUTKU_USERID))
                {
                    ikutkuWorker.SendDirectMessage(_auth.twitterUserid, StringsResource.ACC_READY_MSG);
                }
            }
            catch (TwitterQueryException ex)
            {
                switch (ex.ErrorCode.ToEnum<TwitterErrorCode>())
                {
                    case TwitterErrorCode.DUPLICATE_MESSAGE:
                        break;
                    default:
                        Syslog.Write(ex, "Failed NotifyUserThatSyncIsComplete {0}. {1} {2}", _auth.twitterUsername, ex.ErrorCode, ex.Message);
                        break;
                }
            }
            catch (Exception ex)
            {
                Syslog.Write(ex);
            }
        }

        // we are just adding to the queue here because it may take awhile to clear existing entries
        // faster response time when the user clicks the button
        public void ResetQueue(QueueSettings resetSettings)
        {
            using (var userRepository = new Repository<user>())
            {
                var usr = userRepository.FindById(_auth.twitterUserid);

                // need to remove from followings queue
                RemoveFromFollowingsQueue(false);

                // add to diff queue
                usr.AddToDiffQueue((int)resetSettings);


                usr.settings |= (long) Settings.RESET;
                usr.settings &= ~(long)Settings.UPGRADE;
                userRepository.SaveChanges();
            }
        }

        public void ResetRun()
        {
            // we need this here because of manual resets
            if (_rebuildSettings == QueueSettings.USER_TRIGGERED || _rebuildSettings == QueueSettings.NONE)
            {
                _rebuildSettings = QueueSettings.ALL_BUILDS;
            }

            using (var scopedUnitOfWork = new UnitOfWork())
            {
                scopedUnitOfWork.AutoDetectChangesEnabled = false;
                scopedUnitOfWork.SetDeadlockPriority(DeadlockPriority.HIGH);

                var usr = scopedUnitOfWork.Repository<user>().FindById(_auth.twitterUserid);

                if (_rebuildSettings.HasFlag(QueueSettings.BUILD_FOLLOWERS))
                {
                    scopedUnitOfWork.AutoDetectChangesEnabled = true;
                    usr.followersCursor = null;
                    usr.followerCountTotal = null;
                    usr.followerCountSync = null;
                    scopedUnitOfWork.SaveChanges();
                    scopedUnitOfWork.AutoDetectChangesEnabled = false;

                    while (true)
                    {
                        var followers = scopedUnitOfWork.ExecuteSqlNonQuery(
                            string.Format("delete top (1000) from followers where ownerid = '{0}'",
                                          _auth.twitterUserid));

                        if (followers == 0)
                        {
                            break;
                        }
                    }
                }

                if (_rebuildSettings.HasFlag(QueueSettings.BUILD_FOLLOWINGS))
                {
                    scopedUnitOfWork.AutoDetectChangesEnabled = true;
                    usr.followingsCursor = null;
                    usr.followingCountTotal = null;
                    usr.followingCountSync = null;
                    scopedUnitOfWork.SaveChanges();
                    scopedUnitOfWork.AutoDetectChangesEnabled = false;

                    while (true)
                    {
                        var followings = scopedUnitOfWork.ExecuteSqlNonQuery(
                            string.Format("delete top (1000) from followings where ownerid = '{0}'",
                                          _auth.twitterUserid));

                        if (followings == 0)
                        {
                            break;
                        }
                    }
                }

                if (_rebuildSettings.HasFlag(QueueSettings.BUILD_LISTS))
                {
                    scopedUnitOfWork.AutoDetectChangesEnabled = true;
                    usr.userlistCount = null;
                    scopedUnitOfWork.SaveChanges();
                    scopedUnitOfWork.AutoDetectChangesEnabled = false;

                    scopedUnitOfWork.Repository<usersInList>().DeleteRange(usr.usersInLists);
                    scopedUnitOfWork.Repository<usersList>().DeleteRange(usr.usersLists);
                    scopedUnitOfWork.SaveChanges();
                }

                scopedUnitOfWork.AutoDetectChangesEnabled = true;
                usr.uncachedCount = null;
                usr.uncachedTotal = null;
                usr.uncachedFollowingCount = null;
                usr.uncachedFollowingTotal = null;
                usr.authFailCount = 0;

                // remove reset bit
                usr.queuedUsers.First().settings &= ~((int)QueueSettings.RESET);
                usr.settings &= ~((long)Settings.RESET);

                scopedUnitOfWork.SaveChanges();
            }
        }

        public void StartDiffRebuild()
        {
            Debug.WriteLine("Rebuild BEGIN: Start() ");

            long? followersCursor;
            long? followingsCursor;

            using (var unitOfWork = new UnitOfWork(false))
            {
                var usr = unitOfWork.Repository<user>().FindById(_auth.twitterUserid);
                if (usr == null)
                {
                    return;
                }
                followersCursor = usr.followersCursor;
                followingsCursor = usr.followingsCursor;
            }

            if (_rebuildSettings.HasFlag(QueueSettings.BUILD_FOLLOWINGS))
            {
                BuildFollowings(followingsCursor);
            }

            if (_rebuildSettings.HasFlag(QueueSettings.BUILD_FOLLOWERS))
            {
                BuildFollowers(followersCursor);
            }

            if (_rebuildSettings.HasFlag(QueueSettings.BUILD_LISTS))
            {
                BuildUserLists();
            }

            using (var unitOfWork = new UnitOfWork(false))
            {
                var usr = unitOfWork.Repository<user>().FindById(_auth.twitterUserid);
                if (usr == null)
                {
                    return;
                }
                followersCursor = usr.followersCursor;
                followingsCursor = usr.followingsCursor;
            }

            if (followersCursor.HasValue && followersCursor.Value == 0 &&
                followingsCursor.HasValue && followingsCursor.Value == 0)
            {
                BuildDiffCache();
            }

            if (!string.IsNullOrEmpty(_purgeReason))
            {
                // finish him!!!
                PurgeUser(_purgeReason);
            }
            else if (IsSyncComplete())
            {
                // finally done
                RemoveFromDiffQueue(false);

                if (_userQueued)
                {
                    NotifyUserThatSyncIsComplete();
                }
            }
            else
            {
                if (_queueIntent != null)
                {
                    QueueDiffQueueTimerEvent(_queueIntent.Reason, _queueIntent.SecondsToWait);
                }
            
                // requeue
                RemoveFromDiffQueue(true);
            }

            Debug.WriteLine("Rebuild END: Start() ");
        }

        public void StartFollowingsRebuild()
        {
            var uncachedCount = BuildFollowingsCache();

            if (!string.IsNullOrEmpty(_purgeReason))
            {
                // finish him!!!
                PurgeUser(_purgeReason);
            }
            else if (uncachedCount.HasValue && uncachedCount.Value == 0)
            {
                // completed
                RemoveFromFollowingsQueue(false);
            }
            else
            {
                // requeue
                if (_queueIntent != null)
                {
                    QueueFollowingsQueueTimerEvent(_queueIntent.Reason, _queueIntent.SecondsToWait);
                }

                // requeue
                RemoveFromFollowingsQueue(true);
            }
        }

        public void RemoveFromDiffQueue(bool requeue)
        {
            using (var unitOfWork = new UnitOfWork())
            {
                var usr = unitOfWork.Repository<user>().FindById(_auth.twitterUserid);

                if (usr == null)
                {
                    // user has been purged. nothing to see here people. move along
                    return;
                }

                var q = usr.queuedUsers.SingleOrDefault();

                if (q == null)
                {
                    return;
                }

                unitOfWork.Repository<queuedUser>().Delete(q);
                unitOfWork.SaveChanges();

                if (!requeue)
                {
                    // the end
                    // update time
                    TouchUpdatedDate();

                    if (usr.startTime.HasValue)
                    {
                        var timespan = (DateTime.UtcNow - usr.startTime).Value;

                        usr.lastRebuildDuration = timespan.Ticks;
                        usr.apiNextRetry = null;

                        var newQt = new queueTime
                        {
                            secondsQueued = Convert.ToInt32(timespan.TotalSeconds),
                            created = DateTime.UtcNow.ToDayDate()
                        };

                        unitOfWork.Repository<queueTime>().Insert(newQt);
                    }

                    // add to followings queue
                    usr.AddToFollowingsQueue();
                }
                else
                {
                    usr.AddToDiffQueue(_rebuildSettings.ToInt(), true);
                }

                unitOfWork.SaveChanges();
            }
        }

        public void RemoveFromFollowingsQueue(bool requeue)
        {
            using (var unitOfWork = new UnitOfWork())
            {
                var usr = unitOfWork.Repository<user>().FindById(_auth.twitterUserid);

                if (usr == null)
                {
                    // user has been purged. nothing to see here people. move along
                    return;
                }

                var q = usr.queuedFollowingUsers.SingleOrDefault();

                if (q == null)
                {
                    return;
                }

                unitOfWork.Repository<queuedFollowingUser>().Delete(q);
                unitOfWork.SaveChanges();

                if (requeue)
                {
                    usr.AddToFollowingsQueue();
                }
                else
                {
                    usr.apiNextRetry = null;
                }

                unitOfWork.SaveChanges();
            }
        }

        private void BuildFollowers(long? cursor)
        {
            Debug.WriteLine("Rebuild BEGIN: BuildFollowers({0})", cursor);

            while (!cursor.HasValue || cursor.Value != 0)
            {
                try
                {
                    var graph = _twitterWorker.GetFollowers((cursor ?? -1).ToString());

                    if (graph == null)
                    {
                        //cursor = 0;
                    }
                    else
                    {
                        cursor = long.Parse(graph.CursorMovement.Next);

                        using (var unitOfWork = new UnitOfWork())
                        {
                            var usr = unitOfWork.Repository<user>().FindById(_auth.twitterUserid);

                            var existing = unitOfWork.Repository<follower>()
                                                  .FindAsNoTracking(x => x.ownerid == _auth.twitterUserid)
                                                  .Select(x => x.twitterid)
                                                  .ToArray();

                            var inserts = graph.IDs.Except(existing)
                                               .Select(y => new follower()
                                               {
                                                   ownerid = _auth.twitterUserid,
                                                   twitterid = y
                                               }).ToArray();

                            Debug.WriteLine("BuildFollowers:" + inserts);
                            unitOfWork.Repository<follower>().BulkInsert(inserts, 1000);

                            // save cursor
                            usr.followerCountSync = existing.Length + inserts.Length;
                            usr.followersCursor = cursor;

                            if (cursor == 0)
                            {
                                usr.followerCountTotal = usr.followerCountSync;
                            }
                            unitOfWork.SaveChanges();
                        }
                    }
                }
                catch (TwitterQueryException ex)
                {
                    if (ErrorCheckAndHandle(ex, "BuildFollowers"))
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Syslog.Write(ex, "BuildFollowers");
                }
            } // while

            Debug.WriteLine("Rebuild END: BuildFollowers() ");
        }

        private void BuildFollowings(long? cursor)
        {
            Debug.WriteLine("Rebuild BEGIN: BuildFollowings({0})", cursor);

            while (!cursor.HasValue || cursor.Value != 0)
            {
                try
                {
                    var graph = _twitterWorker.GetFriends((cursor ?? -1).ToString());

                    if (graph == null)
                    {
                        //cursor = 0;
                    }
                    else
                    {
                        cursor = long.Parse(graph.CursorMovement.Next);

                        using (var unitOfWork = new UnitOfWork())
                        {
                            unitOfWork.SetIsolationReadUncommitted();
                            var usr = unitOfWork.Repository<user>().FindById(_auth.twitterUserid);

                            var existing = unitOfWork.Repository<following>()
                                                  .FindAsNoTracking(x => x.ownerid == _auth.twitterUserid)
                                                  .Select(x => x.twitterid)
                                                  .ToArray();

                            var inserts = graph.IDs.Except(existing)
                                .Select(y => new following()
                                {
                                    ownerid = _auth.twitterUserid,
                                    twitterid = y
                                }).ToArray();

                            Debug.WriteLine("BuildFollowings:" + inserts);

                            unitOfWork.Repository<following>().BulkInsert(inserts, 1000);

                            // save cursor
                            usr.followingCountSync = existing.Length + inserts.Length;
                            usr.followingsCursor = cursor;

                            if (cursor == 0)
                            {
                                usr.followingCountTotal = usr.followingCountSync;
                            }
                            unitOfWork.SaveChanges();
                        }
                    }
                }
                catch (TwitterQueryException ex)
                {
                    if (ErrorCheckAndHandle(ex, "BuildFollowings"))
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Syslog.Write(ex, "BuildFollowings");
                }
            }

            Debug.WriteLine("Rebuild END: BuildFollowings() ");
        }

        // only need to build excluded lists
        private void BuildUserLists()
        {
            Debug.WriteLine("Rebuild BEGIN: BuildUserLists() ");

            // get all lists
            PopulateUserLists();

            // find incomplete lists
            using (var unitOfWork = new UnitOfWork(false))
            {
                var incompletelists = unitOfWork.Repository<usersList>()
                                                .FindAsNoTracking(x => x.ownerid == _auth.twitterUserid &&
                                                              (!x.listCursor.HasValue || x.listCursor.Value != 0))
                                                .Select(x => new
                                                {
                                                    x.id,
                                                    x.listCursor
                                                });

                foreach (var list in incompletelists)
                {
                    PopulateUsersInList(list.id, list.listCursor);
                }
            }

            Debug.WriteLine("Rebuild END: BuildUserLists()");
        }

        private void SaveApiStats()
        {
            var apiResults = _twitterWorker.GetApiStats();
            // no more calls left and there is a time to save
            if (apiResults[1] == 0 && apiResults[4] != -1)
            {
                using (var usrRepository = new Repository<user>())
                {
                    var usr = usrRepository.FindById(_auth.twitterUserid);
                    if (usr != null)
                    {
                        usr.apiNextRetry = apiResults[4];
                        usrRepository.SaveChanges(false);
                    }
                }
            }
        }

        private void BuildDiffCache()
        {
            using (var scopedWork = new UnitOfWork())
            {
                scopedWork.SetIsolationReadUncommitted();
                var staleDate = DateTime.UtcNow.AddDays(-General.DB_CACHE_VALID_DAYS);

                var followers = scopedWork.Repository<follower>().FindAll(x => x.ownerid == _auth.twitterUserid)
                                                                                .Select(x => x.twitterid);

                var followings = scopedWork.Repository<following>().FindAll(x => x.ownerid == _auth.twitterUserid)
                                                                                  .Select(x => x.twitterid);

                var cachedUsers = scopedWork.Repository<cachedUser>()
                                            .FindAll(x => x.updated > staleDate)
                                            .Select(x => x.twitterid);

                Debug.WriteLine(string.Format("Followers:{0}, Followings:{1}, Cached:{2}, Intersect: {3}",
                                              followers.Count(),
                                              followings.Count(),
                                              cachedUsers.Count(),
                                              followers.Intersect(followings).Count()));

                var staleAndUncachedTwitterUsersQuery = followers.Except(followings).Union(followings.Except(followers)).Except(cachedUsers);

                var uncachedCount = staleAndUncachedTwitterUsersQuery.Count();
                var usr = scopedWork.Repository<user>().FindById(_auth.twitterUserid);

                usr.uncachedCount = uncachedCount;
                if (usr.uncachedTotal == null || // we only want to set the total the first time around
                    usr.uncachedCount > usr.uncachedTotal) // picks up new entries
                {
                    usr.uncachedTotal = usr.uncachedCount;
                }

                scopedWork.SaveChanges();

                if (uncachedCount != 0)
                {
                    var workingset = staleAndUncachedTwitterUsersQuery.Take(CACHERESOLVER_WORKINGSIZE);

                    // take 100 x 180 / 15   => our API limit for 1 min
                    var workinglist = workingset.ToArray();

                    var page = 0;
                    while (true)
                    {
                        var ids = workinglist.Skip(100 * (page++)).Take(100).ToArray();

                        if (ids.Length == 0)
                        {
                            break;
                        }
                        var lastResolvedCount = 0;

                        try
                        {
                            lastResolvedCount = ResolveProfiles(ids);
                        }
                        catch (TwitterQueryException ex)
                        {
                            if (ErrorCheckAndHandle(ex, "BuildDiffCache"))
                            {
                                break;
                            }
                        }

                        if (lastResolvedCount == 0 ||
                            ids.Length < 100)
                        {
                            break;
                        }
                    }
                    usr.uncachedCount = staleAndUncachedTwitterUsersQuery.Count();
                }
                else
                {
                    usr.uncachedCount = 0;
                }
                scopedWork.SaveChanges(false);
            }

            // add to queue?
            if (_queueIntent != null)
            {
                QueueDiffQueueTimerEvent(_queueIntent.Reason, _queueIntent.SecondsToWait);
            }
        }

        private int? BuildFollowingsCache()
        {
            using (var scopedWork = new UnitOfWork())
            {
                var staleDate = DateTime.UtcNow.AddDays(-General.DB_CACHE_VALID_DAYS);

                var followings = scopedWork.Repository<following>().FindAll(x => x.ownerid == _auth.twitterUserid)
                                                                                  .Select(x => x.twitterid);

                var cachedUsers = scopedWork.Repository<cachedUser>()
                                            .FindAll(x => x.updated > staleDate)
                                            .Select(x => x.twitterid);

                var staleAndUncachedTwitterUsersQuery = followings.Except(cachedUsers);

                var uncachedCount = staleAndUncachedTwitterUsersQuery.Count();
                var usr = scopedWork.Repository<user>().FindById(_auth.twitterUserid);

                usr.uncachedFollowingCount = uncachedCount;
                if (usr.uncachedFollowingTotal == null || // we only want to set the total the first time around
                    usr.uncachedFollowingCount > usr.uncachedFollowingTotal) // picks up new entries
                {
                    usr.uncachedFollowingTotal = usr.uncachedFollowingCount;
                }

                scopedWork.SaveChanges();

                if (uncachedCount != 0)
                {
                    var workingset = staleAndUncachedTwitterUsersQuery
                        .Take(General.DB_CACHERESOLVER_WORKINGSIZE);

                    // take 100 x 180 / 15   => our API limit for 1 min
                    var workinglist = workingset.ToArray();

                    var page = 0;
                    while (true)
                    {
                        var ids = workinglist.Skip(100 * (page++)).Take(100).ToArray();

                        if (ids.Length == 0)
                        {
                            break;
                        }
                        var lastResolvedCount = 0;

                        try
                        {
                            lastResolvedCount = ResolveProfiles(ids);
                        }
                        catch (TwitterQueryException ex)
                        {
                            if (ErrorCheckAndHandle(ex, "BuildFollowingsCache"))
                            {
                                break;
                            }
                        }

                        if (lastResolvedCount == 0 ||
                            ids.Length < 100)
                        {
                            break;
                        }
                    }
                    usr.uncachedFollowingCount = staleAndUncachedTwitterUsersQuery.Count();
                }
                else
                {
                    usr.uncachedFollowingCount = 0;
                }
                scopedWork.SaveChanges(false);

                return usr.uncachedFollowingCount;
            }
        }

        // returns true if no error
        private bool ErrorCheckAndHandle(TwitterQueryException ex, string functionName)
        {
            try
            {
                SaveApiStats();
                ErrorIncrementCounter(ex.ErrorCode);
            }
            catch (Exception cex)
            {
                Syslog.Write(cex);
            }

            var errorcode = ex.ErrorCode.ToEnum<TwitterErrorCode>();

            var isShowStopper = false;

            switch (errorcode)
            {
                case TwitterErrorCode.NO_ERROR:
                    break;
                case TwitterErrorCode.OVERCAPACITY:
                case TwitterErrorCode.NO_REPLY:
                case TwitterErrorCode.INTERNAL_ERROR:
                    _queueIntent = new TimerQueueIntent("capacity", 10);
                    isShowStopper = true;
                    break;
                case TwitterErrorCode.RATE_LIMIT_EXCEEDED:
                    isShowStopper = true;
                    //_queueIntent = new TimerQueueIntent("rate", TwitterWorkerBase.TWITTER_API_WAIT_SECONDS);
                    break;
                case TwitterErrorCode.INVALID_OR_EXPIRED_CREDENTIALS:
                case TwitterErrorCode.PAGE_DOES_NOT_EXIST:
                case TwitterErrorCode.NO_MATCH: 
                case TwitterErrorCode.FAIL_AUTHENTICATION:
                    isShowStopper = true;
                    IncrementAuthFailureCounter(errorcode);
                    break;
                case TwitterErrorCode.USER_SUSPENDED:
                case TwitterErrorCode.ACCOUNT_SUSPENDED:
                    isShowStopper = true;
                    _purgeReason = StringsResource.ACC_RESET_AUTH_MESSAGE;
                    break;
                default:
                    isShowStopper = true;
                    Syslog.Write("{0} :ErrorsCheckAndHandle Error => {1} {2}",
                        _auth.twitterUsername,
                        ex.ErrorCode,
                        ex.Message);
                    break;
            }

            


            return isShowStopper;
        }

        private bool QueueDiffQueueTimerEvent(string reason, double secondsToWait)
        {
            var key = GetTimerKey(_auth.twitterUserid);

            if (TimerWaitQueue.Instance.IsTimerRunning(key))
            {
                return false;
            }

            // only add to wait queue if we are currently in a queue
            using (var unitOfWork = new UnitOfWork())
            {
                var q = unitOfWork.Repository<queuedUser>().FindOneNoTracking(x => x.ownerid == _auth.twitterUserid);
                if (q == null)
                {
                    return false;
                }
            }

            var state = new TimerActionState(key, () => Callback_DiffWaitQueue(_auth.twitterUserid, _rebuildSettings),
                                             reason);

            if (!TimerWaitQueue.Instance.Add(state, TimeSpan.FromSeconds(secondsToWait)))
            {
                Syslog.Write("Failed to queue {0} {1} ({2})", "Start", _auth.twitterUserid, _auth.twitterUsername);
                return false;
            }
            return true;
        }

        private bool QueueFollowingsQueueTimerEvent(string reason, double secondsToWait)
        {
            var key = GetTimerKey(_auth.twitterUserid);

            if (TimerWaitQueue.Instance.IsTimerRunning(key))
            {
                return false;
            }

            // only add to wait queue if we are currently in a queue
            using (var unitOfWork = new UnitOfWork())
            {
                var q = unitOfWork.Repository<queuedFollowingUser>().FindOneNoTracking(x => x.ownerid == _auth.twitterUserid);
                if (q == null)
                {
                    return false;
                }
            }

            var state = new TimerActionState(key, () => Callback_FollowingWaitQueue(_auth.twitterUserid),
                                             reason);

            if (!TimerWaitQueue.Instance.Add(state, TimeSpan.FromSeconds(secondsToWait)))
            {
                Syslog.Write("Failed to queue {0} {1} ({2})", "Start", _auth.twitterUserid, _auth.twitterUsername);
                return false;
            }
            return true;
        }

        private void PopulateUserLists()
        {
            Debug.WriteLine("Rebuild BEGIN: PopulateUserLists() ");

            // all lists. hmmm looks complicated
            try
            {
                using (var unitOfWork = new UnitOfWork())
                {
                    var usr = unitOfWork.Repository<user>().FindById(_auth.twitterUserid);

                    var existingListIDs = unitOfWork.Repository<usersList>()
                               .FindAll(x => x.ownerid == _auth.twitterUserid)
                               .Select(x => x.id)
                               .ToArray();

                    var allTwitterLists = _twitterWorker.GetLists();

                    var twitterListIDs = allTwitterLists.Select(x => x.ListIDResult).ToArray();

                    var deletes = existingListIDs.Where(x => !twitterListIDs.Contains(x)).ToArray();

                    var inserts = twitterListIDs.Where(x => !existingListIDs.Contains(x)).ToArray();

                    var updates = twitterListIDs.Except(inserts.Union(deletes)).ToArray();

                    if (deletes.Any())
                    {
                        unitOfWork.Repository<usersInList>().DeleteRange(usr.usersLists.SelectMany(x => x.usersInLists));
                        unitOfWork.Repository<usersList>().DeleteRange(usr.usersLists);
                    }

                    if (inserts.Any())
                    {
                        var insertrows = allTwitterLists.Where(x => inserts.Contains(x.ListIDResult))
                                                        .Select(x => new usersList
                                                        {
                                                            id = x.ListIDResult,
                                                            exclude = true,
                                                            listname = x.Name,
                                                            slug = x.SlugResult,
                                                            ownerid = _auth.twitterUserid,
                                                            memberCount = x.MemberCount,
                                                            updated = DateTime.UtcNow
                                                        });

                        unitOfWork.Repository<usersList>().BulkInsert(insertrows, 1000);
                    }

                    if (updates.Any())
                    {
                        foreach (var list in allTwitterLists.Where(x => updates.Contains(x.ListIDResult)))
                        {
                            var dbentry = unitOfWork.Repository<usersList>().FindById(list.ListIDResult);
                            if (dbentry != null)
                            {
                                dbentry.listname = list.Name;
                                dbentry.slug = list.SlugResult;
                                dbentry.ownerid = _auth.twitterUserid;
                                dbentry.memberCount = list.MemberCount;
                                dbentry.updated = DateTime.UtcNow;
                            }
                        }
                    }

                    // save list count
                    usr.userlistCount = usr.usersLists.Count;

                    unitOfWork.SaveChanges();
                }
            }
            catch (TwitterQueryException ex)
            {
                ErrorCheckAndHandle(ex, "PopulateUserLists");
            }

            Debug.WriteLine("Rebuild END: PopulateUserLists() ");
        }

        private void PopulateUsersInList(string listid, long? cursor)
        {
            Debug.WriteLine("Rebuild BEGIN: PopulateUsersInList({0} {1})", listid, cursor);

            while (!cursor.HasValue || cursor.Value != 0)
            {
                try
                {
                    using (var unitOfWork = new UnitOfWork())
                    {
                        var existingUserIDs = unitOfWork.Repository<usersInList>()
                            .FindAsNoTracking(x => x.ownerid == _auth.twitterUserid && x.userlistid == listid)
                                      .Select(x => x.twitterid);

                        var result = _twitterWorker.GetListMembers(listid, (cursor ?? -1).ToString());

                        if (result != null)
                        {
                            // get new entries
                            var inserts =
                                result.Users.Where(z => !existingUserIDs.Contains(z.Identifier.ID))
                                    .Select(z => new usersInList()
                                    {
                                        userlistid = listid,
                                        twitterid = z.Identifier.ID,
                                        ownerid = _auth.twitterUserid
                                    });

                            unitOfWork.Repository<usersInList>().BulkInsert(inserts, 1000);

                            // NOTE: don't update userscache as we don't have last tweet info
                            cursor = long.Parse(result.CursorMovement.Next);
                        }
                        else
                        {
                            cursor = 0;
                        }

                        // save cursor
                        var list = unitOfWork.Repository<usersList>().FindById(listid);

                        list.listCursor = cursor;

                        if (cursor == 0)
                        {
                            var twitterCount = list.memberCount;
                            var actualCount = existingUserIDs.Count();

                            if (twitterCount != actualCount)
                            {
                                Syslog.Write("PopulateUsersInList {0}:{1} => Twitter: {2} Actual: {3}",
                                    list.id,
                                    list.slug,
                                    twitterCount,
                                    actualCount);
                            }

                            list.memberCount = actualCount;
                        }
                        unitOfWork.SaveChanges();
                    }
                }
                catch (TwitterQueryException ex)
                {
                    if (ErrorCheckAndHandle(ex, "PopulateUsersInList"))
                    {
                        break;
                    }
                }
            }

            Debug.WriteLine("Rebuild END: PopulateUsersInList({0} {1}) ", listid, cursor);
        }

        public void PurgeUser(string reasonForPurge)
        {
            Debug.Assert(!string.IsNullOrEmpty(_auth.twitterUserid));

            try
            {
                using (var scopedUnitOfWork = new UnitOfWork(true, 30))
                {
                    scopedUnitOfWork.SetDeadlockPriority(DeadlockPriority.LOW);

                    var toBeDeleted = scopedUnitOfWork.Repository<user>().FindById(_auth.twitterUserid);

                    if (toBeDeleted == null)
                    {
                        return;
                    }

                    scopedUnitOfWork.Repository<queuedUser>().DeleteRange(toBeDeleted.queuedUsers);
                    scopedUnitOfWork.Repository<queuedFollowingUser>().DeleteRange(toBeDeleted.queuedFollowingUsers);
                    scopedUnitOfWork.SaveChanges();

                    while (true)
                    {
                        var working = scopedUnitOfWork.ExecuteSqlNonQuery(
                            string.Format("delete top (1000) from followers where ownerid = '{0}'",
                                          _auth.twitterUserid));

                        if (working == 0)
                        {
                            break;
                        }
                    }

                    while (true)
                    {
                        var working = scopedUnitOfWork.ExecuteSqlNonQuery(
                            string.Format("delete top (1000) from followings where ownerid = '{0}'",
                                          _auth.twitterUserid));

                        if (working == 0)
                        {
                            break;
                        }
                    }

                    scopedUnitOfWork.Repository<loginInterval>().DeleteRange(toBeDeleted.loginIntervals);
                    scopedUnitOfWork.Repository<usersInList>().DeleteRange(toBeDeleted.usersInLists);
                    scopedUnitOfWork.Repository<usersList>().DeleteRange(toBeDeleted.usersLists);
                    scopedUnitOfWork.SaveChanges();

                    scopedUnitOfWork.Repository<user>().Delete(toBeDeleted);
                    scopedUnitOfWork.SaveChanges();
                }

                NotifyUserThatAccountHasBeenPurged(reasonForPurge);
            }
            catch (Exception ex)
            {
                Syslog.Write(ex, "PurgeUser fail for user {0}", _auth.twitterUsername);
            }
        }

        private static string GetTimerKey(string twitterid)
        {
            return string.Format("{0}_{1}", General.IKUTKU_SCREENNAME, twitterid);
        }

        private void TouchUpdatedDate()
        {
            using (var unitOfWork = new UnitOfWork())
            {
                var usr = unitOfWork.Repository<user>().FindById(_auth.twitterUserid);
                usr.updated = DateTime.UtcNow;
                unitOfWork.SaveChanges();
            }
        }

        private static void Callback_DiffWaitQueue(string twitterid, QueueSettings qsettings)
        {
            using (var uow = new UnitOfWork())
            {
                var usr = uow.Repository<user>().FindById(twitterid);
                if (usr != null)
                {
                    var worker = new DatabaseWorker(usr.ToAuthInfo(), qsettings);
                    worker.StartDiffRebuild();
                }
            }
        }

        private static void Callback_FollowingWaitQueue(string twitterid)
        {
            using (var uow = new UnitOfWork())
            {
                var usr = uow.Repository<user>().FindById(twitterid);
                if (usr != null)
                {
                    var worker = new DatabaseWorker(usr.ToAuthInfo());
                    worker.StartFollowingsRebuild();
                }
            }
        }

        public int UpdateProfiles(string[] inputIDs, DeadlockPriority priority)
        {
            var resolved = new List<LinqToTwitter.User>();
            try
            {
                // todo: exclude entries where there is already a cacheUser
                var twitterusers = _twitterWorker.GetUsersById(inputIDs);

                // known problem that twitter sometimes returns invalid entries
                resolved.AddRange(twitterusers.Where(x => x.Identifier.ID != ""));
            }
            catch (TwitterQueryException ex)
            {
                var errorCode = ex.ErrorCode.ToEnum<TwitterErrorCode>();
                switch (errorCode)
                {
                    case TwitterErrorCode.USER_SUSPENDED:
                    case TwitterErrorCode.ACCOUNT_SUSPENDED:
                    case TwitterErrorCode.PAGE_DOES_NOT_EXIST:
                    case TwitterErrorCode.FAIL_AUTHENTICATION:
                    case TwitterErrorCode.OVERCAPACITY:
                    //case TwitterErrorCode.NO_REPLY:
                        break;
                    case TwitterErrorCode.RATE_LIMIT_EXCEEDED:
                        SaveApiStats();
                        break;
                    case TwitterErrorCode.INVALID_OR_EXPIRED_CREDENTIALS:
                        IncrementAuthFailureCounter(errorCode);
                        break;
                    default:
                        Syslog.Write("UpdateProfiles Error => {0} {1} :Query => {2}",
                                     ex.ErrorCode,
                                     ex.Message,
                                     _twitterWorker.GetLastUrl());
                        break;
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

            // not everything was resolved
            var unresolvedCount = inputIDs.Length - resolved.Count;
            if (unresolvedCount > 0)
            {
                var unresolved = inputIDs.Except(resolved.Select(x => x.Identifier.UserID));

                // there will be some unresolved due to invalid entries
                foreach (var unresolvedID in unresolved)
                {
                    try
                    {
                        var resolvedUser = _twitterWorker.GetUserById(unresolvedID);

                        if (resolvedUser != null)
                        {
                            resolved.Add(resolvedUser);
                        }
                    }
                    catch (TwitterQueryException ex)
                    {
                        var errorCode = ex.ErrorCode.ToEnum<TwitterErrorCode>();
                        switch (errorCode)
                        {
                            // we add to list to be deleted later and we assume that entry has been resolved
                            case TwitterErrorCode.USER_SUSPENDED:
                            case TwitterErrorCode.PAGE_DOES_NOT_EXIST:
                            case TwitterErrorCode.OVERCAPACITY:
                            case TwitterErrorCode.INVALID_OR_EXPIRED_CREDENTIALS:
                            case TwitterErrorCode.FAIL_AUTHENTICATION:
                            //case TwitterErrorCode.NO_REPLY:
                                break;
                            case TwitterErrorCode.RATE_LIMIT_EXCEEDED:
                                SaveApiStats();
                                break;
                            default:
                                Syslog.Write("UpdateProfiles Error => {0} {1} Limit({2})", ex.ErrorCode, ex.Message,
                                    string.Join(":", _twitterWorker.GetApiStats()));
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Syslog.Write(ex);
                    }
                }
            }

            // update db
            AddOrUpdateUsersCache(resolved.ToArray(), priority);

            return resolved.Count();
        }

        private int ResolveProfiles(string[] inputIDs)
        {
            Debug.WriteLine("Rebuild START: ResolveAndCacheUsersByID() ");

            var resolved = new List<QueryResultContainer>();
            
            try
            {
                // todo: exclude entries where there is already a cacheUser
                var twitterusers = _twitterWorker.GetUsersById(inputIDs);

                // known problem that twitter sometimes returns invalid entries
                resolved.AddRange(twitterusers.Where(x => x.Identifier.ID != "").Select(x => new QueryResultContainer(x)));
            }
            catch (TwitterQueryException ex)
            {
                var errorCode = ex.ErrorCode.ToEnum<TwitterErrorCode>();
                switch (errorCode)
                {
                    case TwitterErrorCode.USER_SUSPENDED:
                    case TwitterErrorCode.ACCOUNT_SUSPENDED:
                    case TwitterErrorCode.PAGE_DOES_NOT_EXIST:
                    case TwitterErrorCode.INTERNAL_ERROR:
                    case TwitterErrorCode.NO_MATCH:
                    case TwitterErrorCode.NO_REPLY:
                        break;
                    case TwitterErrorCode.FAIL_AUTHENTICATION:
                        break;
                    case TwitterErrorCode.INVALID_OR_EXPIRED_CREDENTIALS:
                        IncrementAuthFailureCounter(errorCode);
                        break;
                    case TwitterErrorCode.RATE_LIMIT_EXCEEDED:
                        SaveApiStats();
                        break;
                    case TwitterErrorCode.OVERCAPACITY:
                        break;
                    default:
                        Syslog.Write(ex, "ResolveAndCacheUsersByID Error => {0} {1} :Query => {2}: Limit: {3}",
                                     ex.ErrorCode,
                                     ex.Message,
                                     _twitterWorker.GetLastUrl(),
                                     string.Join(":", _twitterWorker.GetApiStats()));
                        throw;
                }
            }
            catch (Exception ex)
            {
                Syslog.Write(ex);
            }
            
            // not everything was resolved
            var unresolvedCount = inputIDs.Length - resolved.Count(x => x.Valid);
            if (unresolvedCount > 0)
            {
                var unresolved = inputIDs.Except(resolved.Select(x => x.User.Identifier.UserID));

                // there will be some unresolved due to invalid entries
                resolved.AddRange(ResolveIndividualUser(unresolved));
            }

            // update db
            AddOrUpdateUsersCache(resolved.Where(x => x.Valid).Select(x => x.User).ToArray(), DeadlockPriority.NORMAL);

            // delete invalid accounts
            var invalidUsers = resolved.Where(x => !x.Valid).Select(x => x.IdOrName).ToArray();

            if (invalidUsers.Length != 0)
            {
                invalidUsers.RemoveFollowersByIdFromDatabase(_auth.twitterUserid, true);
            }

            Debug.WriteLine("Rebuild END: ResolveAndCacheUsersByID() ");

            return resolved.Count(x => x.Valid);
        }

        private IEnumerable<QueryResultContainer> ResolveIndividualUser(IEnumerable<string> uncachedIDs)
        {
            Debug.WriteLine("Rebuild START: ResolveIndividualUser() ");

            var subsets = uncachedIDs.InSetsOf(10);

            //split into groups of ten
            var resolved = new QueryResultContainer[] { };
            try
            {
                var parallel = new ParallelHelper(TwitterWorkerBase.TASK_TIMEOUT);
                resolved = parallel.ProcessData(subsets, x =>
                {
                    var results = new List<QueryResultContainer>();

                    foreach (var id in x)
                    {
                        parallel.GetCancellationToken().ThrowIfCancellationRequested();
                        try
                        {
                            var resolvedUser = _twitterWorker.GetUserById(id);

                            if (resolvedUser != null)
                            {
                                var result = new QueryResultContainer(resolvedUser);
                                results.Add(result);
                            }
                        }
                        catch (TwitterQueryException ex)
                        {
                            var errorCode = ex.ErrorCode.ToEnum<TwitterErrorCode>();
                            switch (errorCode)
                            {
                                // we add to list to be deleted later and we assume that entry has been resolved
                                case TwitterErrorCode.USER_SUSPENDED:
                                case TwitterErrorCode.PAGE_DOES_NOT_EXIST:
                                    var invalid = new QueryResultContainer(id);
                                    results.Add(invalid);
                                    break;
                                case TwitterErrorCode.RATE_LIMIT_EXCEEDED:
                                    SaveApiStats();
                                    break;
                                case TwitterErrorCode.OVERCAPACITY:
                                    parallel.Cancel();
                                    break;
                                case TwitterErrorCode.INVALID_OR_EXPIRED_CREDENTIALS:
                                case TwitterErrorCode.FAIL_AUTHENTICATION:
                                case TwitterErrorCode.NO_REPLY:
                                    IncrementAuthFailureCounter(errorCode, true);
                                    parallel.Cancel();
                                    break;
                                default:
                                    Syslog.Write("HandleIndividualError Error => {0} {1} Limit({2})", ex.ErrorCode, ex.Message,
                                        string.Join(":", _twitterWorker.GetApiStats()));
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Syslog.Write(ex);
                        }
                    }
                    return results;
                }).ToArray();
            }
            catch (Exception ex)
            {
                if (!(ex is OperationCanceledException))
                {
                    Syslog.Write(ex);
                }
            }

            Debug.WriteLine("Rebuild END: ResolveIndividualUser() ");
            return resolved;
        }

        private void AddOrUpdateUsersCache(LinqToTwitter.User[] inputUsers, DeadlockPriority priority)
        {
            // ensure no duplicates
            var twitterUsers = inputUsers.GroupBy(x => x.Identifier.ID).Select(x => x.First()).ToArray();

            if (twitterUsers.Length != inputUsers.Length)
            {
                Syslog.Write("AddOrUpdateUsersCache:Duplicate IDs => {0}",
                    string.Join(",", inputUsers.GroupBy(x => x.Identifier.ID).Where(x => x.Count() > 1).Select(x => x.Key)));
            }

            var twitterUserids = twitterUsers.Select(y => y.Identifier.ID);

            using (var repository = new Repository<cachedUser>())
            {
                repository.SetDeadlockPriority(priority);
                var existingCachedUsers = repository.FindAll(x => twitterUserids.Contains(x.twitterid))
                                                        .OrderBy(x => x.id);
                try
                {
                    // UPDATE existing cachedUsers first
                    int count = 0;
                    const int batchsize = 100;
                    while (true)
                    {
                        var working = existingCachedUsers.Skip(count++ * batchsize).Take(batchsize);

                        if (!working.Any())
                        {
                            break;
                        }

                        foreach (var row in working)
                        {
                            var usr = twitterUsers.Single(x => x.Identifier.ID == row.twitterid);

                            row.followersCount = usr.FollowersCount;
                            row.followingsCount = usr.FriendsCount;
                            row.ratio = User.CalculateRatio(usr.FollowersCount, usr.FriendsCount);
                            row.screenName = usr.Identifier.ScreenName;
                            row.lastTweet =
                                !string.IsNullOrEmpty(usr.Status.StatusID)
                                    ? usr.Status.CreatedAt
                                    : (DateTime?)null;
                            row.updated = DateTime.UtcNow;
                            row.profileImageUrl = usr.ProfileImageUrl;
                        }
                        repository.SaveChanges(false);
                    }
                }
                catch (Exception ex)
                {
                    Syslog.Write(ex, "AddOrUpdateUsersCache => {0} update FAIL", existingCachedUsers.Count());
                }
            }

            using (var insertWorker = new UnitOfWork(false))
            {
                // INSERT NEW ENTRIES
                var existingCachedUsers = insertWorker.Repository<cachedUser>().FindAsNoTracking(x => twitterUserids.Contains(x.twitterid))
                    .Select(x => x.twitterid);

                var newCachedUsers =
                    twitterUsers.Where(x => !existingCachedUsers.Contains(x.Identifier.ID))
                                .ToDbModel()
                                .ToArray();

                try
                {
                    insertWorker.Repository<cachedUser>().BulkInsert(newCachedUsers, 100);
                }
                catch (Exception ex)
                {
                    Syslog.Write(ex, "AddOrUpdateUsersCache => {0} inserts FAIL", newCachedUsers.Length);
                }
            }
        }

#if DEBUG
        public void DebugMethod1(long? cursor)
        {
            Syslog.Write("DebugMethod1 " + cursor);
        }

        public void DebugMethod2()
        {
            Syslog.Write("DebugMethod2");
        }

        public void StartTest()
        {
            QueueDiffQueueTimerEvent("test", 5);
        }

#endif
        /*
        public void Dispose()
        {
            if (_twitterWorker != null)
            {
                _twitterWorker.Dispose();
            }
        }
         * */
    }
}