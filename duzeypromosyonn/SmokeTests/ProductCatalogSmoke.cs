using System;
using System.IO;
using System.Linq;
using duzeypromosyonn.Models;
using duzeypromosyonn.Services;

namespace SmokeTests
{
    internal static class ProductCatalogSmoke
    {
        private static int Main(string[] args)
        {
            var projectRoot = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
            var xmlPath = Path.Combine(projectRoot, "XML", "urun.xml");
            var service = new ProductCatalogService(xmlPath);

            var catalog = service.GetCatalog();
            if (catalog.Products.Count < 1000)
            {
                Console.Error.WriteLine("XML katalog beklenen urun adedinden az urun dondurdu: " + catalog.Products.Count);
                return 1;
            }

            var query = new ProductQuery
            {
                Q = "usb",
                CategoryId = 76,
                InStock = true,
                Sort = "price-asc",
                Page = 1,
                PageSize = 12
            };

            var result = service.Search(query);
            if (result.TotalCount == 0 || result.Items.Count == 0)
            {
                Console.Error.WriteLine("USB kategori aramasi stoklu urun dondurmedi.");
                return 1;
            }

            if (result.Items.Any(x => x.TotalStock <= 0))
            {
                Console.Error.WriteLine("Stok filtresi stok disi urun dondurdu.");
                return 1;
            }

            if (result.Items.Zip(result.Items.Skip(1), (a, b) => a.Price <= b.Price).Any(x => !x))
            {
                Console.Error.WriteLine("price-asc siralamasi fiyatlari artan dondurmedi.");
                return 1;
            }

            var cufflinkResult = service.Search(new ProductQuery
            {
                Q = "Kol D\u00fc\u011fmesi",
                Page = 1,
                PageSize = 12
            });

            if (cufflinkResult.TotalCount != 2 || cufflinkResult.Items.Any(x => !x.Name.Contains("Kol D\u00fc\u011fmeli")))
            {
                Console.Error.WriteLine("Kol Dugmesi aramasi XML'deki 2 kol dugmeli set urununu dondurmedi: " + cufflinkResult.TotalCount);
                return 1;
            }

            var careSetResult = service.Search(new ProductQuery
            {
                Q = "Bak\u0131m Seti",
                Page = 1,
                PageSize = 12
            });

            if (careSetResult.TotalCount == 0 || careSetResult.Items.Any(x => !x.Name.Contains("Manik\u00fcr Seti")))
            {
                Console.Error.WriteLine("Bakim Seti aramasi XML'deki manikur seti urunlerini dondurmedi: " + careSetResult.TotalCount);
                return 1;
            }

            Console.WriteLine("ProductCatalogService XML, filtre ve siralama smoke testi gecti.");
            return 0;
        }
    }
}
