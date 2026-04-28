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
                Console.Error.WriteLine("XML katalog beklenen ürün adedinden az ürün döndürdü: " + catalog.Products.Count);
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
                Console.Error.WriteLine("USB kategori araması stoklu ürün döndürmedi.");
                return 1;
            }

            if (result.Items.Any(x => x.TotalStock <= 0))
            {
                Console.Error.WriteLine("Stok filtresi stok dışı ürün döndürdü.");
                return 1;
            }

            if (result.Items.Zip(result.Items.Skip(1), (a, b) => a.Price <= b.Price).Any(x => !x))
            {
                Console.Error.WriteLine("price-asc sıralaması fiyatları artan döndürmedi.");
                return 1;
            }

            Console.WriteLine("ProductCatalogService XML, filtre ve sıralama smoke testi geçti.");
            return 0;
        }
    }
}
