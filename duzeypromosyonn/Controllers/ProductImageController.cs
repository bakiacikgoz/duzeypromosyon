using System;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using duzeypromosyonn.Services;

namespace duzeypromosyonn.Controllers
{
    public class ProductImageController : Controller
    {
        [Route("urun-gorsel/{id:int}/{size}", Name = "ProductImage")]
        public ActionResult Product(int id, string size)
        {
            var product = CatalogService().GetById(id);
            var image = ImageCache().GetImage(product, size);

            if (image.IsPlaceholder)
            {
                Response.Cache.SetCacheability(HttpCacheability.NoCache);
                Response.Cache.SetNoStore();
                Response.Cache.SetMaxAge(TimeSpan.Zero);
                Response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));
            }
            else
            {
                Response.Cache.SetCacheability(HttpCacheability.Public);
                Response.Cache.SetExpires(DateTime.UtcNow.AddDays(30));
                Response.Cache.SetMaxAge(TimeSpan.FromDays(30));
                Response.Cache.SetValidUntilExpires(true);
            }

            return File(image.Path, image.ContentType);
        }

        private ProductCatalogService CatalogService()
        {
            return new ProductCatalogService(Server.MapPath("~/XML/urun.xml"));
        }

        private ProductImageCacheService ImageCache()
        {
            return new ProductImageCacheService(
                Server.MapPath("~/Content/ProductImages"),
                Server.MapPath("~/Content/promosyon 2.2/img/kategori-8.jpg"));
        }

        public static void QueueWarmup(HttpServerUtility server, int maxProducts)
        {
            if (server == null || maxProducts <= 0)
            {
                return;
            }

            var xmlPath = server.MapPath("~/XML/urun.xml");
            var cacheFolder = server.MapPath("~/Content/ProductImages");
            var placeholderPath = server.MapPath("~/Content/promosyon 2.2/img/kategori-8.jpg");

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var catalog = new ProductCatalogService(xmlPath).GetCatalog();
                    var imageCache = new ProductImageCacheService(cacheFolder, placeholderPath);
                    imageCache.WarmCardImages(catalog.Products.Take(maxProducts));
                }
                catch
                {
                    // Warm-up is opportunistic and must not affect the application lifecycle.
                }
            });
        }

        public static int WarmupProductLimit()
        {
            int value;
            return int.TryParse(ConfigurationManager.AppSettings["ProductImageWarmupLimit"], out value) && value > 0
                ? value
                : 72;
        }
    }
}
