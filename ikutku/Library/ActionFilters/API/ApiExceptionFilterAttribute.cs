using System.Net;
using System.Net.Http;
using System.Web.Http.Filters;
using clearpixels.Logging;

namespace ikutku.Library.ActionFilters.API
{
    public class ApiExceptionFilterAttribute : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext context)
        {
            if (context.Exception == null)
            {
                return;
            }

            Syslog.Write(context.Exception);
            context.Response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }
    }
}