using System.Web.Mvc;
using ikutku.Library.ActionFilters;

namespace ikutku
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new MvcExceptionFilterAttribute());
            filters.Add(new HandleErrorAttribute());
        }
    }
}