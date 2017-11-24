using System;
using System.Linq;
using System.Threading;
using System.Web.Mvc;
using clearpixels.Logging;
using ikutku.DB;
using ikutku.DB.extension;
using ikutku.Library.ActionFilters;
using ikutku.Library.Workers;
using ikutku.Models.queue;
using ikutku.Models.sync;
using ikutku.Models.user;
using ikutku.ViewModels.dashboard;

namespace ikutku.Controllers
{
    [MvcAuthorize]
    public class dashboardController : baseController
    {
        private readonly IUnitOfWork _unitOfWork;   

        public dashboardController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _unitOfWork.SetIsolationReadUncommitted();
        }

        protected override void Dispose(bool disposing)
        {
            _unitOfWork.Dispose();
            base.Dispose(disposing);
        }

        public ActionResult Index()
        {
            ViewBag.Title = "Dashboard";

            // check if rebuild currently in progress
            var auth = Thread.CurrentPrincipal.ToAuthInfo();
            var usr = _unitOfWork.Repository<user>().FindById(auth.twitterUserid);

            var worker = new DatabaseWorker(auth);

            // no need to check all followings queue
            if (!worker.IsDiffDatabaseReady())
            {
                var diffQueue = _unitOfWork.Repository<queuedUser>()
                                           .FindAsNoTracking()
                                           .OrderBy(x => x.id)
                                           .Select(x => x.ownerid)
                                           .ToArray();

                var diffProgress = usr.GetDiffProgressModel(diffQueue);

                var viewmodel = new NotReadyViewModel
                    {
                        diffProgress = diffProgress,
                        followingProgress = new Progress(),
                        NotificationOff = ((Settings) usr.settings).HasFlag(Settings.NO_DIRECT_MSG) ||
                                          worker.IsFollowingIkutku
                    };

                ViewBag.Title = "Updating Dashboard";

                return View("notready", viewmodel);
            }

            // do not automatically resync database because loop may occur if twitter account is popular and keeps getting new followers
            usr.TouchLastLoginAndOtherThings(_unitOfWork);

            try
            {
                _unitOfWork.SaveChanges();
            }
            catch (Exception ex)
            {
                _unitOfWork.Dispose();
                Syslog.Write(ex);
            }

            // clear old cookies
            ClearOldCookies();

            var settings = worker.IsAccountUpToDate(true);
            ViewBag.photoUrl = usr.photoUrl;
            ViewBag.twitterUsername = usr.username;
            ViewBag.followerCount = usr.followerCountTotal;
            ViewBag.followingCount = usr.followingCountTotal;

            ViewBag.OutOfSync = settings != QueueSettings.NONE;

            return View();
        }

        private void ClearOldCookies()
        {
            var cookieNames = new[] { "auth_id", "auth_id2", "auth_token", "auth_token2", "pageMeNoFollow", "pageNoFollowMe" };

            foreach (var entry in cookieNames)
            {
                var cookie = Response.Cookies[entry];
                if (cookie != null)
                {
                    cookie.Expires = DateTime.Now.AddMonths(-1);
                }
            }
        }
    }
}
