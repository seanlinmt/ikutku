using System;
using System.Net;
using System.Web.Mvc;
using Elmah;
using clearpixels.Helpers.exceptions;
using clearpixels.Logging;

namespace ikutku.Library.ActionFilters
{
    public class MvcExceptionFilterAttribute : ActionFilterAttribute, IExceptionFilter
    {
        public void OnException(ExceptionContext filterContext)
        {
            if (filterContext.ExceptionHandled || filterContext.Exception == null)
            {
                return;
            }

            Syslog.Write(filterContext.Exception);

            if (!filterContext.HttpContext.Request.IsAjaxRequest())
            {
                filterContext.Result = new RedirectResult("/error");
            }
            else
            {
                if (filterContext.Exception is ClearpixelsException)
                {
                    filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable, filterContext.Exception.Message);
                }
                else if (filterContext.Exception is TimeoutException ||
                filterContext.Exception is OperationCanceledException)
                {
                    filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.GatewayTimeout, "Operation timeout");
                }
                else
                {
                    filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Oops. An error has occurred.");
                }
            }
            filterContext.ExceptionHandled = true;
        }
    }
}
