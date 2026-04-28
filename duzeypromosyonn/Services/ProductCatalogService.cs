using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using duzeypromosyonn.Models;

namespace duzeypromosyonn.Services
{
    public class ProductCatalogService
    {
        private static readonly object CacheLock = new object();
        private static string _cachedPath;
        private static DateTime _cachedWriteTime;
        private static ProductCatalog _cachedCatalog;
        private readonly string _xmlPath;

        public ProductCatalogService(string xmlPath)
        {
            _xmlPath = xmlPath;
        }

        public ProductCatalog GetCatalog()
        {
            var fileInfo = new FileInfo(_xmlPath);
            if (!fileInfo.Exists)
            {
                return new ProductCatalog();
            }

            lock (CacheLock)
            {
                if (_cachedCatalog != null
                    && string.Equals(_cachedPath, _xmlPath, StringComparison.OrdinalIgnoreCase)
                    && _cachedWriteTime == fileInfo.LastWriteTimeUtc)
                {
                    return _cachedCatalog;
                }

                var products = ParseProducts(_xmlPath);
                var catalog = BuildCatalog(products, fileInfo.LastWriteTime);
                _cachedPath = _xmlPath;
                _cachedWriteTime = fileInfo.LastWriteTimeUtc;
                _cachedCatalog = catalog;
                return catalog;
            }
        }

        public ProductSearchResult Search(ProductQuery query)
        {
            query = query ?? new ProductQuery();
            var catalog = GetCatalog();
            var products = catalog.Products.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var needle = Normalize(query.Q);
                products = products.Where(product =>
                    Contains(product.Name, needle)
                    || Contains(product.GroupCode, needle)
                    || Contains(product.CategoryMain, needle)
                    || Contains(product.CategorySub, needle)
                    || product.Options.Any(option => Contains(option.StockCode, needle)));
            }

            CategoryInfo selectedCategory = null;
            if (query.CategoryId.HasValue && query.CategoryId.Value > 0)
            {
                selectedCategory = catalog.Categories.FirstOrDefault(category => category.Id == query.CategoryId.Value);
                products = products.Where(product => MatchesCategory(product, query.CategoryId.Value)
                    || (selectedCategory != null && string.Equals(product.CategoryMain, selectedCategory.Name, StringComparison.OrdinalIgnoreCase)));
            }

            var categoryScopedProducts = products.ToList();
            var subCategories = categoryScopedProducts.Select(product => product.CategorySub)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .ToList();

            if (!string.IsNullOrWhiteSpace(query.AltKategori))
            {
                products = products.Where(product => string.Equals(product.CategorySub, query.AltKategori, StringComparison.OrdinalIgnoreCase));
            }

            if (query.MinPrice.HasValue)
            {
                products = products.Where(product => product.Price >= query.MinPrice.Value);
            }

            if (query.MaxPrice.HasValue)
            {
                products = products.Where(product => product.Price <= query.MaxPrice.Value);
            }

            if (query.InStock.HasValue)
            {
                products = query.InStock.Value
                    ? products.Where(product => product.TotalStock > 0 || product.Options.Any(option => option.StockQuantity > 0))
                    : products.Where(product => product.TotalStock <= 0 && product.Options.All(option => option.StockQuantity <= 0));
            }

            var colorScopedProducts = products.ToList();
            var colors = colorScopedProducts.SelectMany(product => product.Options.Select(option => option.Color))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .ToList();

            if (!string.IsNullOrWhiteSpace(query.Color))
            {
                products = products.Where(product => product.Options.Any(option => string.Equals(option.Color, query.Color, StringComparison.OrdinalIgnoreCase)));
            }

            products = ApplySort(products, query.Sort);

            var filtered = products.ToList();
            var totalCount = filtered.Count;
            var pageSize = query.EffectivePageSize;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            var page = Math.Min(query.EffectivePage, totalPages);
            var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new ProductSearchResult
            {
                Items = items,
                TotalCount = totalCount,
                TotalPages = totalPages,
                Page = page,
                PageSize = pageSize,
                Query = query,
                Categories = catalog.Categories,
                SubCategories = subCategories.Any() ? subCategories : catalog.SubCategories,
                Colors = colors.Any() ? colors : catalog.Colors,
                MinPrice = catalog.MinPrice,
                MaxPrice = catalog.MaxPrice
            };
        }

        public Product GetById(int id)
        {
            return GetCatalog().Products.FirstOrDefault(product => product.Id == id);
        }

        public static IList<Product> ParseProducts(string xmlPath)
        {
            var document = XDocument.Load(xmlPath);
            var nodes = document.Descendants("Urun");
            var products = new List<Product>();

            foreach (var node in nodes)
            {
                try
                {
                    var options = node.Element("UrunSecenek") == null
                        ? new List<ProductOption>()
                        : node.Element("UrunSecenek").Elements("Secenek").Select(ParseOption).ToList();

                    var totalStock = ParseInt(Value(node, "TumVaryantToplamStokAdedi"));
                    if (totalStock == 0 && options.Any())
                    {
                        totalStock = options.Sum(option => option.StockQuantity);
                    }

                    products.Add(new Product
                    {
                        Id = ParseInt(Value(node, "UrunKartiID")),
                        Name = Value(node, "UrunAdi"),
                        GroupCode = Value(node, "UrunGrupKodu"),
                        IntroHtml = Value(node, "OnYazi"),
                        DescriptionHtml = Value(node, "Aciklama"),
                        CategoryId = ParseInt(Value(node, "KategoriID")),
                        MainCategoryId = ParseInt(Value(node, "KategoriIDAna")),
                        SubCategoryId = ParseInt(Value(node, "KategoriIDAlt")),
                        CategoryMain = Value(node, "KategoriMain"),
                        CategorySub = Value(node, "KategoriSub"),
                        Unit = Value(node, "SatisBirimi"),
                        SourceUrl = Value(node, "UrunUrl"),
                        ImageUrl = Value(node, "ResimUrl"),
                        Price = ParseDecimal(Value(node, "SatisFiyati")),
                        Currency = string.IsNullOrWhiteSpace(Value(node, "ParaBirimi")) ? "TL" : Value(node, "ParaBirimi"),
                        VatIncluded = string.Equals(Value(node, "KDVDahil"), "TRUE", StringComparison.OrdinalIgnoreCase),
                        VatRate = ParseInt(Value(node, "KdvOrani")),
                        TotalStock = totalStock,
                        SupplierCode = Value(node, "TedarikciKodu"),
                        Options = options
                    });
                }
                catch
                {
                    // Ignore malformed product rows so one bad XML item cannot take down the catalog.
                }
            }

            return products.Where(product => product.Id > 0 && !string.IsNullOrWhiteSpace(product.Name)).ToList();
        }

        public static string Slugify(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "urun";
            }

            var normalized = text.Trim().ToLower(new CultureInfo("tr-TR"));
            var replacements = new Dictionary<string, string>
            {
                {"ş", "s"}, {"ğ", "g"}, {"ı", "i"}, {"i̇", "i"}, {"ö", "o"}, {"ü", "u"}, {"ç", "c"}
            };

            foreach (var replacement in replacements)
            {
                normalized = normalized.Replace(replacement.Key, replacement.Value);
            }

            normalized = Regex.Replace(normalized, @"[^a-z0-9]+", "-");
            normalized = Regex.Replace(normalized, @"-+", "-").Trim('-');
            return string.IsNullOrWhiteSpace(normalized) ? "urun" : normalized;
        }

        private static ProductOption ParseOption(XElement optionNode)
        {
            return new ProductOption
            {
                VariationId = ParseInt(Value(optionNode, "VaryasyonID")),
                StockCode = Value(optionNode, "StokKodu"),
                StockQuantity = ParseInt(Value(optionNode, "StokAdedi")),
                Price = ParseDecimal(Value(optionNode, "SatisFiyati")),
                Color = Value(optionNode.Element("EkSecenekOzellik"), "Ozellik")
            };
        }

        private static ProductCatalog BuildCatalog(IList<Product> products, DateTime lastWriteTime)
        {
            var categories = products
                .Where(product => product.CategoryId > 0 && !string.IsNullOrWhiteSpace(product.CategoryMain))
                .GroupBy(product => Normalize(product.CategoryMain))
                .Select(group =>
                {
                    var name = group.GroupBy(product => product.CategoryMain)
                        .OrderByDescending(nameGroup => nameGroup.Count())
                        .Select(nameGroup => nameGroup.Key)
                        .FirstOrDefault();
                    var id = group.GroupBy(product => product.CategoryId)
                        .OrderByDescending(idGroup => idGroup.Count())
                        .ThenBy(idGroup => idGroup.Key)
                        .Select(idGroup => idGroup.Key)
                        .FirstOrDefault();

                    return new CategoryInfo
                    {
                        Id = id,
                        Name = name,
                        Count = group.Count(),
                        Slug = Slugify(name)
                    };
                })
                .OrderBy(category => category.Name)
                .ToList();

            var prices = products.Select(product => product.Price).Where(price => price > 0).ToList();

            return new ProductCatalog
            {
                Products = products,
                Categories = categories,
                SubCategories = products.Select(product => product.CategorySub)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value)
                    .ToList(),
                Colors = products.SelectMany(product => product.Options.Select(option => option.Color))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value)
                    .ToList(),
                MinPrice = prices.Any() ? prices.Min() : 0,
                MaxPrice = prices.Any() ? prices.Max() : 0,
                LastXmlWriteTime = lastWriteTime
            };
        }

        private static IEnumerable<Product> ApplySort(IEnumerable<Product> products, string sort)
        {
            switch ((sort ?? string.Empty).ToLowerInvariant())
            {
                case "price-asc":
                    return products.OrderBy(product => product.Price);
                case "price-desc":
                    return products.OrderByDescending(product => product.Price);
                case "name-asc":
                    return products.OrderBy(product => product.Name);
                case "stock-desc":
                    return products.OrderByDescending(product => product.TotalStock);
                default:
                    return products.OrderBy(product => product.Name);
            }
        }

        private static bool MatchesCategory(Product product, int categoryId)
        {
            if (categoryId == 22 || categoryId == 94)
            {
                return product.CategoryId == 22
                    || product.CategoryId == 94
                    || string.Equals(product.CategoryMain, "Kristal ve Ödül Ürünleri", StringComparison.OrdinalIgnoreCase);
            }

            return product.CategoryId == categoryId || product.MainCategoryId == categoryId || product.SubCategoryId == categoryId;
        }

        private static bool Contains(string value, string normalizedNeedle)
        {
            return !string.IsNullOrWhiteSpace(value) && Normalize(value).Contains(normalizedNeedle);
        }

        private static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.ToLower(new CultureInfo("tr-TR"));
        }

        private static string Value(XElement node, string name)
        {
            if (node == null)
            {
                return string.Empty;
            }

            var element = node.Element(name);
            return element == null ? string.Empty : (element.Value ?? string.Empty).Trim();
        }

        private static int ParseInt(string value)
        {
            int result;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : 0;
        }

        private static decimal ParseDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            decimal result;
            if (decimal.TryParse(value, NumberStyles.Number, new CultureInfo("tr-TR"), out result))
            {
                return result;
            }

            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }

            return 0;
        }
    }
}
