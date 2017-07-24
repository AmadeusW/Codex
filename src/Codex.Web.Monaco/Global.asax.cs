using System;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Autofac;
using Autofac.Integration.Mvc;
using Codex.ObjectModel;
using Codex.Storage;

namespace WebUI
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            RegisterDependencyInjection();
            //AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        private void RegisterDependencyInjection()
        {
            var builder = new ContainerBuilder();

            builder.RegisterControllers(typeof(MvcApplication).Assembly);

            // TODO: use a config entry for this
            //builder.RegisterType<Newtonsoft.Json.JsonSerializer>()
            //    .AsSelf()
            //    .SingleInstance();

            builder.Register(_ => new ElasticsearchStorage("http://localhost:9200", requiresProjectGraph: true))
                .As<IStorage>()
                .SingleInstance();

            DependencyResolver.SetResolver(new AutofacDependencyResolver(builder.Build()));
        }
    }
}
