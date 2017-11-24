using System.Linq;
using System.Net;
using System.Threading;
using System.Web.Mvc;
using ikutku.Constants;
using ikutku.DB;
using ikutku.Library.ActionFilters;
using ikutku.Library.Workers;
using ikutku.Models.user;
using ikutku.ViewModels;
using user = ikutku.DB.user;

namespace ikutku.Controllers
{
    [MvcAuthorizeAttribute]
    public class listsController : baseController
    {
        private readonly IUnitOfWork _unitOfWork;

        public listsController(IUnitOfWork unitOfWork)
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
            var auth = Thread.CurrentPrincipal.ToAuthInfo();
            var worker = new DatabaseWorker(auth);
            if (!worker.IsDiffDatabaseReady())
            {
                if (Request.IsAjaxRequest())
                {
                    return new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable, StringsResource.DB_SYNCING);
                }
                return new RedirectResult("/dashboard");
            }

            var existingUser = _unitOfWork.Repository<user>().FindOneNoTracking(x => x.id == auth.twitterUserid);

            // display entries in db
            var viewmodel = new UserListViewModel(auth.settings.HasFlag(Settings.EXCLUDED_HIDE))
            {
                lists = existingUser.usersLists.Select(x => x.ToModel()).ToArray()
            };

            return View(viewmodel);
        }
    }
}
