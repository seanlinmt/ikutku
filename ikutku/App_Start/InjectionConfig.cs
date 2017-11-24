using System.Web.Http;
using System.Web.Mvc;
using ikutku.DB;

namespace ikutku.App_Start
{
    public static class InjectionConfig
    {
        public static void Initialise()
        {
            var container = UnityExtension.BuildUnityContainer();

            // mvc
            DependencyResolver.SetResolver(new Microsoft.Practices.Unity.Mvc.UnityDependencyResolver(container));

            // webAPI
            GlobalConfiguration.Configuration.DependencyResolver = new Microsoft.Practices.Unity.WebApi.UnityDependencyResolver(container);
        }

        
    }
}