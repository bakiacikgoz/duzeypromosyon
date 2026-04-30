using System;
using System.Configuration;
using System.Threading;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using duzeypromosyonn.Controllers;
using duzeypromosyonn.Services;

namespace duzeypromosyonn
{
    public class MvcApplication : System.Web.HttpApplication
    {
        private static Timer _xmlTimer;

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            StartXmlUpdater();
            ProductImageController.QueueWarmup(Server, ProductImageController.WarmupProductLimit());
        }

        protected void Application_End()
        {
            if (_xmlTimer != null)
            {
                _xmlTimer.Dispose();
                _xmlTimer = null;
            }
        }

        private void StartXmlUpdater()
        {
            _xmlTimer = new Timer(_ =>
            {
                try
                {
                    var service = new ProductXmlUpdateService(
                        Server.MapPath("~/XML/urun.xml"),
                        Server.MapPath("~/App_Data/xml-update-status.json"),
                        ConfigurationManager.AppSettings["ProductXmlSourceUrl"]);
                    var status = service.UpdateNow();
                    if (status != null && string.IsNullOrWhiteSpace(status.LastError))
                    {
                        ProductImageController.QueueWarmup(Server, ProductImageController.WarmupProductLimit());
                    }
                }
                catch
                {
                    // Background refresh errors are captured by ProductXmlUpdateService when possible.
                }
            }, null, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1));
        }
    }
}
