using System.Web.Mvc;
using System.Web.Routing;

namespace duzeypromosyonn
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.MapMvcAttributeRoutes();

            routes.MapRoute(
                name: "ProductDetails",
                url: "urun/{title}/{id}",
                defaults: new { controller = "DPromosyon", action = "Detay", title = UrlParameter.Optional },
                constraints: new { id = @"\d+" }
            );

            routes.MapRoute(
                name: "BagDetails",
                url: "canta/{title}/{id}",
                defaults: new { controller = "DPromosyon", action = "CantaDetay", title = UrlParameter.Optional },
                constraints: new { id = @"\d+" }
            );

            routes.MapRoute(
                name: "Category",
                url: "kategori/{title}/{id}",
                defaults: new { controller = "DPromosyon", action = "kategorim", title = UrlParameter.Optional },
                constraints: new { id = @"\d+" }
            );

            routes.MapRoute(
                name: "Catalog",
                url: "urunler",
                defaults: new { controller = "DPromosyon", action = "Urunler" }
            );

            routes.MapRoute(
                name: "Search",
                url: "ara",
                defaults: new { controller = "DPromosyon", action = "Ara" }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "DPromosyon", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
