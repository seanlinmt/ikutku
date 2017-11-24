using System.Data.Entity;
using ikutku.Constants;
using Microsoft.Practices.Unity;

namespace ikutku.DB
{
    public static class UnityExtension
    {
        public static IUnityContainer BuildUnityContainer()
        {
            var container = new UnityContainer();

            // register all your components with the container here
            // e.g. container.RegisterType<ITestService, TestService>();            
            container.RegisterType<DbContext, ikutkuEntities>();
            container.RegisterType<IUnitOfWork, UnitOfWork>(new InjectionConstructor(true, General.DB_COMMAND_TIMEOUT));

            return container;
        }
    }
}
