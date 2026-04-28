using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using System.Web.UI;
using duzeypromosyonn.Models;
using duzeypromosyonn.Services;

namespace duzeypromosyonn.Controllers
{
    public class DPromosyonController : Controller
    {
        private const string SliderFile = "sliders.json";
        private const string CantaFile = "cantas.json";

        [OutputCache(Duration = 300, VaryByParam = "*", Location = OutputCacheLocation.ServerAndClient)]
        public ActionResult Index()
        {
            var catalog = CatalogService().GetCatalog();
            var featured = CatalogService().Search(new ProductQuery { InStock = true, Page = 1, PageSize = 12, Sort = "name-asc" });

            var model = new HomeViewModel
            {
                Sliders = Store().Load<Slider>(SliderFile).OrderBy(x => x.DisplayOrder).ToList(),
                FeaturedProducts = featured.Items,
                Categories = catalog.Categories.OrderByDescending(x => x.Count).ToList(),
                TotalProducts = catalog.Products.Count,
                XmlStatus = XmlUpdateService().GetStatus()
            };

            return View(model);
        }

        [Route("urunler")]
        [OutputCache(Duration = 300, VaryByParam = "*", Location = OutputCacheLocation.ServerAndClient)]
        public ActionResult Urunler(ProductQuery query)
        {
            query = NormalizeQuery(query);
            var result = CatalogService().Search(query);
            return View("Urunler", result);
        }

        [Route("kategori/{title}/{id}")]
        [OutputCache(Duration = 300, VaryByParam = "*", Location = OutputCacheLocation.ServerAndClient)]
        public ActionResult kategorim(string title, int id, ProductQuery query)
        {
            query = NormalizeQuery(query);
            query.CategoryId = id;
            var result = CatalogService().Search(query);
            return View("Urunler", result);
        }

        [Route("ara")]
        public ActionResult Ara(string searchTerm, string q, int page = 1)
        {
            var queryText = string.IsNullOrWhiteSpace(q) ? searchTerm : q;
            return RedirectToAction("Urunler", new { q = queryText, page = page });
        }

        [Route("urun/{title}/{id}")]
        public ActionResult Detay(string title, int id)
        {
            var catalogService = CatalogService();
            var catalog = catalogService.GetCatalog();
            var product = catalog.Products.FirstOrDefault(x => x.Id == id);
            if (product == null)
            {
                return HttpNotFound();
            }

            var related = catalog.Products
                .Where(x => x.Id != product.Id)
                .Where(x => x.CategoryId == product.CategoryId
                    || string.Equals(x.CategoryMain, product.CategoryMain, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(product.CategorySub) && string.Equals(x.CategorySub, product.CategorySub, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(x => string.Equals(x.CategoryMain, product.CategoryMain, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(x => !string.IsNullOrWhiteSpace(product.CategorySub) && string.Equals(x.CategorySub, product.CategorySub, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(x => x.TotalStock)
                .ThenBy(x => x.Name)
                .Take(8)
                .ToList();

            return View(new ProductDetailViewModel
            {
                Product = product,
                RelatedProducts = related
            });
        }

        [Route("canta/{title}/{id}")]
        public ActionResult CantaDetay(string title, int id)
        {
            var canta = Store().Load<Canta>(CantaFile).FirstOrDefault(x => x.Id == id);
            if (canta == null)
            {
                return HttpNotFound();
            }

            return View(canta);
        }

        [HttpGet]
        [Route("iletisim")]
        public ActionResult iletisim()
        {
            return View(new ContactViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("iletisim")]
        public ActionResult iletisim(ContactViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            TempData["SuccessMessage"] = "Mesajınız alındı. Ekibimiz en kısa sürede size dönüş yapacak.";
            return RedirectToAction("iletisim");
        }

        public ActionResult Katalog()
        {
            return Redirect("~/Content/2026-Katalog.pdf");
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

        private ProductQuery NormalizeQuery(ProductQuery query)
        {
            query = query ?? new ProductQuery();
            var values = Request == null ? null : Request.QueryString;
            if (values == null)
            {
                return query;
            }

            query.Q = FirstText(query.Q, values["q"]);
            query.AltKategori = FirstText(query.AltKategori, values["altKategori"]);
            query.Color = FirstText(query.Color, values["color"]);
            query.Sort = FirstText(query.Sort, values["sort"]);
            query.CategoryId = query.CategoryId ?? ParseNullableInt(values["kategoriId"]);
            query.MinPrice = query.MinPrice ?? ParseNullableDecimal(values["minPrice"]);
            query.MaxPrice = query.MaxPrice ?? ParseNullableDecimal(values["maxPrice"]);

            if (query.Page <= 0)
            {
                query.Page = ParseInt(values["page"], 1);
            }

            if (query.PageSize <= 0)
            {
                query.PageSize = ParseInt(values["pageSize"], 24);
            }

            if (!query.InStock.HasValue)
            {
                query.InStock = ParseNullableBool(values["inStock"]);
            }

            return query;
        }

        private static string FirstText(string current, string incoming)
        {
            return !string.IsNullOrWhiteSpace(current) ? current.Trim() : (incoming ?? string.Empty).Trim();
        }

        private static int ParseInt(string value, int fallback)
        {
            int result;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) && result > 0
                ? result
                : fallback;
        }

        private static int? ParseNullableInt(string value)
        {
            int result;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) && result > 0
                ? (int?)result
                : null;
        }

        private static decimal? ParseNullableDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            decimal result;
            if (decimal.TryParse(value, NumberStyles.Number, new CultureInfo("tr-TR"), out result)
                || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }

            return null;
        }

        private static bool? ParseNullableBool(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
                || value == "1";
        }
    }
}
