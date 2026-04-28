using System.Configuration;
using System.Linq;
using System.Web.Mvc;
using duzeypromosyonn.Models;
using duzeypromosyonn.Services;

namespace duzeypromosyonn.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        public ActionResult Index()
        {
            var catalog = CatalogService().GetCatalog();
            var model = new AdminDashboardViewModel
            {
                ProductCount = catalog.Products.Count,
                CategoryCount = catalog.Categories.Count,
                SliderCount = Store().Load<Slider>("sliders.json").Count,
                CantaCount = Store().Load<Canta>("cantas.json").Count,
                XmlStatus = XmlUpdateService().GetStatus()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Update()
        {
            var status = XmlUpdateService().UpdateNow();
            if (string.IsNullOrWhiteSpace(status.LastError))
            {
                TempData["SuccessMessage"] = "XML kataloğu güncellendi. Ürün sayısı: " + status.LastProductCount;
            }
            else
            {
                TempData["ErrorMessage"] = "XML güncellenemedi: " + status.LastError;
            }

            return RedirectToAction("Index");
        }

        private ProductCatalogService CatalogService()
        {
            return new ProductCatalogService(Server.MapPath("~/XML/urun.xml"));
        }

        private ProductXmlUpdateService XmlUpdateService()
        {
            return new ProductXmlUpdateService(
                Server.MapPath("~/XML/urun.xml"),
                Server.MapPath("~/App_Data/xml-update-status.json"),
                ConfigurationManager.AppSettings["ProductXmlSourceUrl"]);
        }

        private JsonDataStore Store()
        {
            return new JsonDataStore(Server.MapPath("~/App_Data"));
        }
    }
}
