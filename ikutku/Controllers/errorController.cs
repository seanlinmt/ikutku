using System.Net;
using System.Web.Mvc;

namespace ikutku.Controllers
{

    public class errorController : Controller
    {
        public ActionResult Busy()
        {
            ViewBag.Title = "Busy | ikutku";
            return View();
        }

        public ActionResult Index()
        {
            ViewBag.Title = "Oops | ikutku";
            Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            return View();
        }

        public ActionResult NotFound()
        {
            ViewBag.Title = "Page Not Found | ikutku";
            // manifest file will fail if not commented out
            Response.StatusCode = (int)HttpStatusCode.NotFound;

            return View();
        }
    }
}
