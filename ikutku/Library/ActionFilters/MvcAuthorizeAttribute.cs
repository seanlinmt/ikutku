using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using clearpixels.Logging;

namespace ikutku.Library.ActionFilters
{
    public class MvcAuthorizeAttribute : AuthorizeAttribute
    {
        // the following might not be necessary
        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            Syslog.Write("WebAuthorizeAttribute here!");
            if (filterContext.HttpContext.Request.IsAjaxRequest())
            {
                if (filterContext.HttpContext.User.Identity.IsAuthenticated == false)
                {
                    filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Unauthorized);
                }
                else
                {
                    filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Forbidden);
                }

                filterContext.HttpContext.Response.SuppressFormsAuthenticationRedirect = true;
                return;
            }

            // this just sets result to HttpUnauthorizedResult
            base.HandleUnauthorizedRequest(filterContext);
        }

        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext == null)
            {
                throw new ArgumentNullException("filterContext");
            }


            if (filterContext.HttpContext.Request.IsAjaxRequest())
            {
                if (!filterContext.HttpContext.User.Identity.IsAuthenticated)
                {
                    filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Unauthorized);
                    filterContext.HttpContext.Response.SuppressFormsAuthenticationRedirect = true;
                }
            }
            else
            {
                if (!filterContext.HttpContext.User.Identity.IsAuthenticated)
                {
                    filterContext.Result = new RedirectResult("/");
                }
            }
        }
    }
}