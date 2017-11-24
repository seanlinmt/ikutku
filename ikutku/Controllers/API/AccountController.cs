using System;
using System.Linq;
using System.Management.Instrumentation;
using System.Threading;
using LinqToTwitter;
using clearpixels.Logging;
using ikutku.Constants;
using ikutku.DB;
using ikutku.DB.extension;
using ikutku.Library.Workers;
using ikutku.Models.queue;
using ikutku.Models.sync;
using ikutku.Models.user;

namespace ikutku.Controllers.API
{
    public class AccountController : baseApiController
    {
        public AccountController(IUnitOfWork unitOfWork) :base(unitOfWork)
        {
        }


        // GET api/account/
        public Progress GetAccountStatus(AccountStatus type)
        {
            var auth = Thread.CurrentPrincipal.ToAuthInfo();

            if (auth == null)
            {
                return null;
            }

            _unitOfWork.SetIsolationReadUncommitted();

            using (var userRepository = new Repository<user>())
            {
                userRepository.ProxyCreationEnabled = false;
                var usr = userRepository.FindById(auth.twitterUserid);
                if (usr != null)
                {
                    switch (type)
                    {
                        case AccountStatus.DIFFQUEUE:
                            using (var queueRepository = new Repository<queuedUser>())
                            {
                                queueRepository.ProxyCreationEnabled = false;

                                var queuedOwners = queueRepository.FindAsNoTracking()
                                                                  .OrderBy(x => x.id)
                                                                  .Select(x => x.ownerid)
                                                                  .ToArray();

                                var diffProgress = usr.GetDiffProgressModel(queuedOwners);

                                return diffProgress;
                            }
                        case AccountStatus.FOLLOWINGSQUEUE:
                            using (var followingsQueueRepository = new Repository<queuedFollowingUser>())
                            {
                                var queuedOwners = followingsQueueRepository.FindAsNoTracking()
                                                                            .Where(x => x.user.oauthSecret != null)
                                                                            .OrderBy(x => x.id)
                                                                            .Select(x => x.ownerid)
                                                                            .ToArray();

                                var followingsProgress = usr.GetFollowingsProgressModel(queuedOwners);

                                return followingsProgress;
                            }
                        default:
                            throw new ArgumentOutOfRangeException("type");
                    }
                }
            }

            throw new InstanceNotFoundException("user");
        }

        public long DeleteAccountSettings(long id)
        {
            var auth = Thread.CurrentPrincipal.ToAuthInfo();

            var usr = _unitOfWork.Repository<user>().FindById(auth.twitterUserid);

            usr.settings &= ~id;

            _unitOfWork.SaveChanges();

            return id;
        }

        public long PostAccountSettings(long id)
        {
            var auth = Thread.CurrentPrincipal.ToAuthInfo();

            var usr = _unitOfWork.Repository<user>().FindById(auth.twitterUserid);

            usr.settings |= id;

            var worker = new TwitterWorker(auth);

            try
            {
                if (!worker.IsFollowing(General.IKUTKU_USERID))
                {
                    worker.FollowByTwitterUserId(General.IKUTKU_USERID);
                }
            }
            catch (TwitterQueryException ex)
            {
                usr.authFailCount++;
                Syslog.Write(ex);
            }

            _unitOfWork.SaveChanges();

            return id;
        }

        public string PutAccountReset()
        {
            string replyString;

            var auth = Thread.CurrentPrincipal.ToAuthInfo();

            var q = _unitOfWork.Repository<queuedUser>().FindOneNoTracking(x => x.ownerid == auth.twitterUserid);

            if (q != null)
            {
                replyString = "Rebuilding already in progress";
            }
            else
            {
                var builder = new DatabaseWorker(auth);
                var resetSettings = builder.IsAccountUpToDate();
                builder.ResetQueue(resetSettings | QueueSettings.USER_TRIGGERED);

                replyString = "Rebuild started. You will be notified when rebuild is complete.";
            }

            return replyString;
        }
    }
}
