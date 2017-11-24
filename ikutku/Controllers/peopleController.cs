using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Web.Mvc;
using ikutku.Constants;
using ikutku.DB;
using ikutku.Library.ActionFilters;
using ikutku.Library.Workers;
using ikutku.Models.json;
using ikutku.Models.queue;
using ikutku.Models.user;
using ikutku.Models.user.followers;
using ikutku.ViewModels.action;

namespace ikutku.Controllers
{
    [MvcAuthorizeAttribute]
    public class peopleController : baseController
    {
        private readonly IUnitOfWork _unitOfWork;

        public peopleController(IUnitOfWork unitOfWork)
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
            return View();
        }

        public ActionResult ProfileActions(string id)
        {
            var auth = Thread.CurrentPrincipal.ToAuthInfo();
            var viewmodel = new UserActionViewModel();

            
            // get cached user details
            var cachedUsr = _unitOfWork.Repository<cachedUser>().FindOneNoTracking(x => x.twitterid == id);

            if (cachedUsr == null)
            {
                throw new Exception();
            }

            viewmodel.id = cachedUsr.twitterid;
            viewmodel.screenName = cachedUsr.screenName;

            // user lists and what list is user in
            var lists = _unitOfWork.Repository<usersList>().FindAsNoTracking(x => x.ownerid == auth.twitterUserid)
                .Select(x => new UserListMemberActionViewModel()
                    {
                        listid = x.id,
                        name = x.listname,
                        member = x.usersInLists.Any(y => y.twitterid == cachedUsr.twitterid)
                    });

            viewmodel.lists.AddRange(lists);

            return View(viewmodel);
        }

        [HttpGet]
        public ActionResult ListFollowers(FollowersListingType type, string m, OrderByType method, string dir, int page, int rows = 100)
        {
            bool more = !string.IsNullOrEmpty(m);
            bool finito = false;

            var auth = Thread.CurrentPrincipal.ToAuthInfo();

            var worker = new DatabaseWorker(auth);

            var usr = _unitOfWork.Repository<user>().FindById(auth.twitterUserid);
            if ((usr.settings & (long)Settings.UPGRADE) != 0)
            {
                usr.settings &= ~(long) Settings.UPGRADE;
                _unitOfWork.SaveChanges();
                worker.ResetQueue(QueueSettings.ALL_BUILDS | QueueSettings.USER_TRIGGERED);
                return Redirect("/dashboard");
            }

            switch (type)
            {
                case FollowersListingType.MENOFOLLOW:
                case FollowersListingType.NOFOLLOWME:
                    if (!worker.IsDiffDatabaseReady())
                    {
                        if (Request.IsAjaxRequest())
                        {
                            return new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable, StringsResource.DB_SYNCING);
                        }
                        return new RedirectResult("/dashboard");
                    }
                    break;
                case FollowersListingType.ALLFOLLOWINGS:
                    if (!worker.IsAllFollowingsDatabaseReady())
                    {
                        return new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable, StringsResource.DB_SYNCING);
                    }
                    break;
                case FollowersListingType.ALLFOLLOWERS:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException("type");
            }

            // page results if page is specified

            int total = 0;

            var users = worker.ListUserDIffv2(type,
                                                 page++,
                                                 rows,
                                                 method,
                                                 dir,
                                                 auth.settings.HasFlag(Settings.EXCLUDED_HIDE),
                                                 out total);
            
            if (users.Length < rows)
            {
                finito = true;
            }

            var viewmodel = new FollowersListing()
                                {
                                    followers = users,
                                    hasMore = !finito,
                                    showHeader = !more,
                                    count = total,
                                    page = page
                                };

            var view = RenderPartial("content", viewmodel);

            var jsonmodel = new ResultJSON();
            jsonmodel.AddOKData(view);

            return Json(jsonmodel, JsonRequestBehavior.AllowGet);
        }
    }
}
