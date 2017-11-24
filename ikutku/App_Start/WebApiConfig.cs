using System;
using System.Web.Http;
using WebApiContrib.Caching;
using WebApiContrib.MessageHandlers;

namespace ikutku.App_Start
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {

            config.Routes.MapHttpRoute(
                name: "ListFollowersAPI",
                routeTemplate: "api/{controller}/{listid}/{action}/{followerid}",
                defaults: new { listid = @"\d+", followerid = @"\d+" }
            );

            config.Routes.MapHttpRoute(
                name: "DefaultAPI",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            config.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.LocalOnly;

            // API rate limiter
            config.MessageHandlers.Add(new ThrottlingHandler(new InMemoryThrottleStore(), id => 100, TimeSpan.FromMinutes(1)));

            
        }
    }
}