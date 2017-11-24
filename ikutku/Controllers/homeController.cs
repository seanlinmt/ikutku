using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using clearpixels.Helpers.database;
using clearpixels.Helpers.datetime;
using ikutku.Constants;
using ikutku.DB;
using ikutku.DB.extension;
using ikutku.Library.ActionFilters;
using ikutku.Library.Scheduler;
using ikutku.Library.Workers;
using ikutku.Models.queue;
using ikutku.Models.twitter;
using ikutku.Models.user;

namespace ikutku.Controllers
{
    public class homeController : baseController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IQueryable<user> _errs;
        private readonly string[] _waiting;

        public homeController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _unitOfWork.SetIsolationReadUncommitted();
            _unitOfWork.SetDeadlockPriority(DeadlockPriority.LOW);

            _errs =
                _unitOfWork.Repository<user>()
                           .FindAll(
                               x => x.oauthSecret != null &&
                                    (
                                     !x.followersCursor.HasValue || x.followersCursor.Value != 0 ||
                                     !x.followingsCursor.HasValue || x.followingsCursor.Value != 0 ||
                                     !x.uncachedCount.HasValue || x.uncachedCount != 0 ||
                                     (x.followingCountTotal.Value != x.followerCountTotal.Value && !x.followers.Any() &&
                                      !x.followings.Any())
                                      ) &&
                                    !_waiting.Contains(x.id) &&
                                    !x.queuedUsers.Any() &&
                                    (x.settings & (long)Settings.UPGRADE) == 0);
            _waiting = BackgroundScheduler.Instance.GetWaitingWorkerIDs();
        }

        protected override void Dispose(bool disposing)
        {
            _unitOfWork.Dispose();
            base.Dispose(disposing);
        }

        public ActionResult Index()
        {
            // redirect to dashboard if already login
            if (Thread.CurrentPrincipal.Identity.IsAuthenticated)
            {
                return Redirect("/dashboard");
            }

            ViewBag.Title = "ikutku | Manage Twitter Followers";
            if (Session[General.SESSION_ERRORMESSAGE] != null)
            {
                ViewBag.ErrorMessage = Session[General.SESSION_ERRORMESSAGE].ToString();
            }
            
            return View("Welcome");
        }

        public ActionResult Running()
        {
            _unitOfWork.SetIsolationReadUncommitted();
            _unitOfWork.SetDeadlockPriority(DeadlockPriority.LOW);

            var builder = new StringBuilder();
            
            builder.Append("<table>");
            builder.Append("<tr><td style='width:200px'></td><td style='width:200px'></td><td style='width:200px'></td><td style='width:200px'></td></tr>");
    
            var loginIntervals = _unitOfWork.Repository<loginInterval>().FindAll();

            if (loginIntervals.Any())
            {
                builder.Append("<tr><td colspan='4'><strong>Time in between logins</strong></td></tr>");
                builder.Append("<tr><td></td><td>Min</td><td>Mean</td><td>Max</td></tr>");

                builder.AppendFormat("<tr><td></td><td>{0}</td><td>{1}</td><td>{2}</td></tr>",
                    new TimeSpan(loginIntervals.Min(x => x.timeBetweenLogins)).ToReadableString(),
                    new TimeSpan(Convert.ToInt64(loginIntervals.Average(x => x.timeBetweenLogins))).ToReadableString(),
                    new TimeSpan(loginIntervals.Max(x => x.timeBetweenLogins)).ToReadableString());
            }

            var total = _unitOfWork.Repository<queueTime>().FindAll();
            if (total.Any())
            {
                builder.Append("<tr><td colspan='4'><strong>Time in queue  (seconds)</strong></td></tr>");
                builder.Append("<tr><td></td><td>Min</td><td>Mean</td><td>Max</td></tr>");

                builder.AppendFormat("<tr><td>All</td><td>{0}</td><td>{1}</td><td>{2}</td></tr>",
                    new TimeSpan(0, 0, 0, total.Min(x => x.secondsQueued)).ToReadableString(),
                    new TimeSpan(0, 0, 0, Convert.ToInt32(total.Average(x => x.secondsQueued))).ToReadableString(),
                    new TimeSpan(0, 0, 0, total.Max(x => x.secondsQueued)).ToReadableString());
            }

            var currentDate = DateTime.UtcNow.ToDayDate();
            var today = _unitOfWork.Repository<queueTime>().FindAll(x => x.created == currentDate);

            if (today.Any())
            {
                builder.AppendFormat("<tr><td>Today</td><td>{0}</td><td>{1}</td><td>{2}</td></tr>",
                new TimeSpan(0, 0, 0, today.Min(x => x.secondsQueued)).ToReadableString(),
                new TimeSpan(0, 0, 0, Convert.ToInt32(today.Average(x => x.secondsQueued))).ToReadableString(),
                new TimeSpan(0, 0, 0, today.Max(x => x.secondsQueued)).ToReadableString());
            }

            builder.Append("</table>");
            builder.Append("<br/>");

            var errCounters = _unitOfWork.Repository<errorCounter>().FindAll().OrderBy(x => x.type).ToArray();

            builder.Append(string.Join(", ", errCounters.Select(x => string.Format("{0}:{1}", (TwitterErrorCode) x.type, x.count))));

            builder.Append("<br/>");
            builder.Append("<br/>");

            builder.AppendFormat("1:{0} 5:{1} 30:{2} 60:{3}",
                HttpRuntime.Cache[CacheTimerType.Minute1.ToString()] != null?"yes":"no",
                HttpRuntime.Cache[CacheTimerType.Minute5.ToString()] != null ? "yes" : "no",
                HttpRuntime.Cache[CacheTimerType.Minute30.ToString()] != null ? "yes" : "no",
                HttpRuntime.Cache[CacheTimerType.Minute60.ToString()] != null ? "yes" : "no");

            builder.Append("<br/>");
            builder.Append("<br/>");

            var authfails = _unitOfWork.Repository<user>()
                                       .FindAll(x => x.authFailCount != 0)
                                       .OrderByDescending(x => x.authFailCount)
                                       .Take(10)
                                       .Select(x => new {x.username, x.id, x.authFailCount})
                                       .ToArray();

            builder.AppendFormat("auth fails => {0}", string.Join(", ", authfails.Select(x => string.Format("{0}:{1}:{2}", x.username, x.id, x.authFailCount))));

            builder.Append("<br/>");
            builder.Append("<br/>");

            builder.AppendFormat("{1} Waiting: {0}<br/>",
                                             string.Join(", ", BackgroundScheduler.Instance.GetWaitingWorkers()),
                                             BackgroundScheduler.Instance.GetWaitingWorkers().Count());

            builder.AppendFormat("InProgress Queue: {0}<br/>",
                                 string.Join(", ", BackgroundScheduler.Instance.GetInProgressWorkerIDs()));

            var diffQueue = _unitOfWork.Repository<queuedUser>().FindAll(x => !_waiting.Contains(x.ownerid) && x.user.oauthSecret != null)
                .OrderBy(x => x.id)
                .Select(x => new { x.user.username, x.user.id, x.settings }).ToArray();

            builder.AppendFormat("{1} DiffQueued: {0}<br/>",
                string.Join(", ", diffQueue.Select(x => string.Format("<span title='{1}'>{0}({2})</span>", x.username, x.id, (QueueSettings)x.settings))),
                diffQueue.Length);

            var followingsQueue = _unitOfWork.Repository<queuedFollowingUser>().FindAll(x => !_waiting.Contains(x.ownerid) && x.user.oauthSecret != null)
                .OrderBy(x => x.id)
                .Select(x => new { x.user.username, x.user.id, loggedout = x.user.oauthSecret == null }).ToArray();

            builder.AppendFormat("{1} FollowingsQueued: {0}<br/>",
                string.Join(", ", followingsQueue.Select(x => string.Format("<span title='{1}'>{0}</span>", x.username, x.id))),
                followingsQueue.Length);

            var errs = _errs.Select(x => new { x.username, x.id })
                               .ToArray();

            builder.AppendFormat("{1} Error: {0}<br/>",
                                             string.Join(", ", errs.Select(x => string.Format("{0}:{1}", x.username, x.id))),
                                             errs.Length);

            return Content(builder.ToString());
        }

        [MvcAuthorize]
        public ActionResult Reset2(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                var usr = _unitOfWork.Repository<user>().FindById(id);
                if (usr == null)
                {
                    return Content("user not found");
                }
                var builder = new DatabaseWorker(usr.ToAuthInfo());
                builder.RemoveFromDiffQueue(false);
                var resetSettings = builder.IsAccountUpToDate();
                builder.ResetQueue(resetSettings | QueueSettings.USER_TRIGGERED);
            }
            else
            {
                AuthInfo auth = Thread.CurrentPrincipal.ToAuthInfo();
                var builder = new DatabaseWorker(auth);
                builder.RemoveFromDiffQueue(false);
                var resetSettings = builder.IsAccountUpToDate();
                builder.ResetQueue(resetSettings | QueueSettings.USER_TRIGGERED);
            }

            return Content("done");
        }

        public ActionResult ResetSpecific(QueueSettings id)
        {
            AuthInfo auth = Thread.CurrentPrincipal.ToAuthInfo();
            var builder = new DatabaseWorker(auth);
            builder.RemoveFromDiffQueue(false);
            builder.ResetQueue(id | QueueSettings.USER_TRIGGERED);

            return Content("done");
        }

        [MvcAuthorize]
        public ActionResult Purge2(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                var usr = _unitOfWork.Repository<user>().FindById(id);
                if (usr == null)
                {
                    return Content("user not found");
                }
                var builder = new DatabaseWorker(usr.ToAuthInfo());
                builder.PurgeUser("Account deleted by administrator");
            }
            else
            {
                var auth = Thread.CurrentPrincipal.ToAuthInfo();
                var builder = new DatabaseWorker(auth);
                builder.PurgeUser("Account deleted by administrator");
            }

            return Content("done");
        }

        [MvcAuthorize]
        public ActionResult PurgeStale(int id = 100)
        {
            var staleDate = DateTime.UtcNow.AddDays(-(General.DB_ACCOUNT_VALID_DAYS + 1));

            _unitOfWork.SetDeadlockPriority(DeadlockPriority.LOW);
            _unitOfWork.ExecuteSqlNonQuery(string.Format("delete top(100) from cachedUsers where updated < '{0}'", staleDate));
            return Content("done");
        }

        [MvcAuthorize]
        public ActionResult RequeueErrors()
        {
            foreach (var err in _errs)
            {
                var worker = new DatabaseWorker(err.ToAuthInfo());
                var resetSettings = worker.IsAccountUpToDate();
                worker.ResetQueue(resetSettings);
            }

            return Content("done");
        }
    }
}
