using System;
using System.Collections.Generic;
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

            var imageProduct = catalog.Products.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.ImageUrl));
            if (imageProduct == null || string.IsNullOrWhiteSpace(imageProduct.ImageVersion)
                || !imageProduct.ImageVersion.StartsWith("img-v2-", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Urun gorsel versiyonu eski tarayici cache'lerini kirmak icin img-v2- ile baslamali.");
                return 1;
            }

            var imageControllerPath = Path.Combine(projectRoot, "Controllers", "ProductImageController.cs");
            if (File.Exists(imageControllerPath)
                && File.ReadAllText(imageControllerPath).Contains("[OutputCache"))
            {
                Console.Error.WriteLine("ProductImageController placeholder cevaplarini cache'lememek icin OutputCache attribute kullanmamali.");
                return 1;
            }

            if (typeof(ProductImageFile).GetProperty("IsPlaceholder") == null)
            {
                Console.Error.WriteLine("ProductImageFile placeholder cevaplarini controller'da ayirmak icin IsPlaceholder bilgisi tasimali.");
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

            if (new ProductQuery { PageSize = 72 }.EffectivePageSize != 36)
            {
                Console.Error.WriteLine("Sayfa basi urun siniri 36 olmali.");
                return 1;
            }

            var expectedAwardCodes = new[]
            {
                "5984", "1224-DA", "4180", "1224-TA", "4185", "1224-DK", "9501", "9508", "9510", "9511",
                "1224-TK", "9529", "9530", "9528", "9532", "9533", "9534", "9536", "90373"
            };

            var awardResult = service.Search(new ProductQuery
            {
                CategoryId = 22,
                Page = 1,
                PageSize = 36
            });

            if (!MatchesExpectedCodes(awardResult, expectedAwardCodes) || awardResult.Items.Any(x => x.GroupCode == "2608"))
            {
                Console.Error.WriteLine("Kristal ve Odul kategorisi Ilpen 77 sayfasindaki 19 urunle eslesmedi. Gelen: "
                    + string.Join(", ", awardResult.Items.Select(x => x.GroupCode)));
                return 1;
            }

            var publicAwardResult = service.Search(new ProductQuery
            {
                CategoryId = 77,
                Page = 1,
                PageSize = 36
            });

            if (!MatchesExpectedCodes(publicAwardResult, expectedAwardCodes))
            {
                Console.Error.WriteLine("Ilpen public kategori ID 77 Kristal ve Odul urunlerini dondurmedi. Gelen: "
                    + string.Join(", ", publicAwardResult.Items.Select(x => x.GroupCode)));
                return 1;
            }

            var awardCategory = catalog.Categories.FirstOrDefault(x =>
                x.Id == 22 || string.Equals(x.Name, "Kristal ve \u00d6d\u00fcl \u00dcr\u00fcnleri", StringComparison.OrdinalIgnoreCase));
            if (awardCategory == null || awardCategory.Count != expectedAwardCodes.Length)
            {
                Console.Error.WriteLine("Kristal ve Odul kategori sayaci 19 olmali. Gelen: "
                    + (awardCategory == null ? "kategori yok" : awardCategory.Count.ToString()));
                return 1;
            }

            var blackStockResult = service.Search(new ProductQuery
            {
                Color = "Siyah",
                InStock = true,
                Page = 1,
                PageSize = 96
            });

            if (blackStockResult.Items.Any(x => x.Id == 5295))
            {
                Console.Error.WriteLine("Renk + stok filtresi secili renkte stogu olmayan urunu dondurdu: 5295.");
                return 1;
            }

            var categoryResult = service.Search(new ProductQuery
            {
                CategoryId = 7,
                Page = 1,
                PageSize = 12
            });
            var variantCountProperty = typeof(ProductSearchResult).GetProperty("TotalVariantCount");
            if (variantCountProperty == null)
            {
                Console.Error.WriteLine("ProductSearchResult.TotalVariantCount eksik.");
                return 1;
            }

            var expectedVariantCount = catalog.Products
                .Where(x => string.Equals(x.CategoryMain, "Kalemler", StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Options.Count);
            var actualVariantCount = (int)variantCountProperty.GetValue(categoryResult, null);
            if (actualVariantCount != expectedVariantCount)
            {
                Console.Error.WriteLine("Kategori varyant sayisi hatali. Beklenen: " + expectedVariantCount + ", gelen: " + actualVariantCount);
                return 1;
            }

            var stockFixturePath = Path.Combine(Path.GetTempPath(), "duzey-stock-fixture-" + Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(stockFixturePath, @"<?xml version=""1.0"" encoding=""utf-8""?>
<Root>
  <Urunler>
    <Urun>
      <UrunKartiID>900001</UrunKartiID>
      <UrunAdi>Stok Test Urunu</UrunAdi>
      <UrunGrupKodu>STOK-TEST</UrunGrupKodu>
      <KategoriID>7</KategoriID>
      <KategoriIDAna>0</KategoriIDAna>
      <KategoriIDAlt>7</KategoriIDAlt>
      <KategoriMain>Kalemler</KategoriMain>
      <KategoriSub>Metal Kalemler</KategoriSub>
      <ResimUrl>https://b2b.ilpen.com.tr/test.jpg</ResimUrl>
      <SatisFiyati>10,00</SatisFiyati>
      <ParaBirimi>TL</ParaBirimi>
      <TumVaryantToplamStokAdedi>999</TumVaryantToplamStokAdedi>
      <UrunSecenek>
        <Secenek>
          <VaryasyonID>1</VaryasyonID>
          <StokKodu>STOK-TEST SIYAH</StokKodu>
          <StokAdedi>3</StokAdedi>
          <SatisFiyati>10,00</SatisFiyati>
          <EkSecenekOzellik><Ozellik Tanim=""Renk"" Deger=""Siyah"">Siyah</Ozellik></EkSecenekOzellik>
        </Secenek>
        <Secenek>
          <VaryasyonID>2</VaryasyonID>
          <StokKodu>STOK-TEST BEYAZ</StokKodu>
          <StokAdedi>4</StokAdedi>
          <SatisFiyati>10,00</SatisFiyati>
          <EkSecenekOzellik><Ozellik Tanim=""Renk"" Deger=""Beyaz"">Beyaz</Ozellik></EkSecenekOzellik>
        </Secenek>
      </UrunSecenek>
    </Urun>
  </Urunler>
</Root>");
            try
            {
                var fixtureProduct = ProductCatalogService.ParseProducts(stockFixturePath).Single();
                if (fixtureProduct.TotalStock != 7)
                {
                    Console.Error.WriteLine("Varyantli urunde toplam stok varyant toplamindan hesaplanmadi: " + fixtureProduct.TotalStock);
                    return 1;
                }
            }
            finally
            {
                File.Delete(stockFixturePath);
            }

            Console.WriteLine("ProductCatalogService XML, filtre ve siralama smoke testi gecti.");
            return 0;
        }

        private static bool MatchesExpectedCodes(ProductSearchResult result, IEnumerable<string> expectedCodes)
        {
            var expected = expectedCodes.OrderBy(x => x).ToList();
            var actual = result.Items.Select(x => x.GroupCode).OrderBy(x => x).ToList();
            return result.TotalCount == expected.Count && expected.SequenceEqual(actual);
        }
    }
}
