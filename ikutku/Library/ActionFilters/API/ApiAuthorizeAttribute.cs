using System.Threading;
using System.Web.Http;
using ikutku.Models.user;

namespace ikutku.Library.ActionFilters.API
{
    public class ApiAuthorizeAttribute : AuthorizeAttribute
    {
        //http://stackoverflow.com/questions/14871925/where-should-i-plugin-the-authorization-in-asp-net-webapi
        // http://stackoverflow.com/questions/10379002/custom-authorization-attribute-on-asp-net-webapi
        public override void OnAuthorization(System.Web.Http.Controllers.HttpActionContext actionContext)
        {
            base.OnAuthorization(actionContext);
        }
    }
}