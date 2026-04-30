using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using duzeypromosyonn.Models;

namespace duzeypromosyonn.Services
{
    public class ProductCatalogService
    {
        private static readonly object CacheLock = new object();
        private static readonly CategoryAliasRule[] CategoryAliases =
        {
            new CategoryAliasRule(
                "Kristal ve \u00d6d\u00fcl \u00dcr\u00fcnleri",
                22,
                new[] { 22, 77, 94 },
                MatchesAwardCategoryProduct)
        };

        private const string ImageVersionPrefix = "img-v2-";

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
                var needle = NormalizeSearchText(query.Q);
                var needleTokens = SearchTokens(query.Q);
                products = products.Where(product =>
                    MatchesSearch(product.Name, needle, needleTokens)
                    || MatchesSearch(product.GroupCode, needle, needleTokens)
                    || MatchesSearch(product.CategoryMain, needle, needleTokens)
                    || MatchesSearch(product.CategorySub, needle, needleTokens)
                    || MatchesSearch(product.SupplierCode, needle, needleTokens)
                    || MatchesSearch(product.SourceUrl, needle, needleTokens)
                    || product.Options.Any(option => MatchesSearch(option.StockCode, needle, needleTokens)));
            }

            CategoryInfo selectedCategory = null;
            if (query.CategoryId.HasValue && query.CategoryId.Value > 0)
            {
                selectedCategory = FindSelectedCategory(catalog, query.CategoryId.Value);
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
                products = products.Where(product => MatchesStockFilter(product, query.InStock.Value, query.Color));
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
            var totalVariantCount = filtered.Sum(product => product.Options.Count);
            var pageSize = query.EffectivePageSize;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            var page = Math.Min(query.EffectivePage, totalPages);
            var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new ProductSearchResult
            {
                Items = items,
                TotalCount = totalCount,
                TotalVariantCount = totalVariantCount,
                TotalPages = totalPages,
                Page = page,
                PageSize = pageSize,
                Query = query,
                SelectedCategory = selectedCategory,
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

                    var totalStock = options.Any()
                        ? options.Sum(option => option.StockQuantity)
                        : ParseInt(Value(node, "TumVaryantToplamStokAdedi"));
                    var imageUrl = Value(node, "ResimUrl");

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
                        ImageUrl = imageUrl,
                        ImageVersion = ShortHash(imageUrl),
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

            ApplyCategoryAliasCounts(categories, products);

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

        private static bool MatchesStockFilter(Product product, bool inStock, string color)
        {
            if (product == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(color))
            {
                var matchingOptions = product.Options
                    .Where(option => string.Equals(option.Color, color, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (!matchingOptions.Any())
                {
                    return false;
                }

                return inStock
                    ? matchingOptions.Any(option => option.StockQuantity > 0)
                    : matchingOptions.All(option => option.StockQuantity <= 0);
            }

            var hasStock = product.TotalStock > 0 || product.Options.Any(option => option.StockQuantity > 0);
            return inStock ? hasStock : !hasStock;
        }

        private static bool MatchesCategory(Product product, int categoryId)
        {
            var alias = FindCategoryAlias(categoryId);
            if (alias != null)
            {
                return alias.Matches(product);
            }

            if (categoryId == 22 || categoryId == 94)
            {
                return product.CategoryId == 22
                    || product.CategoryId == 94
                    || string.Equals(product.CategoryMain, "Kristal ve Ödül Ürünleri", StringComparison.OrdinalIgnoreCase);
            }

            return product.CategoryId == categoryId || product.MainCategoryId == categoryId || product.SubCategoryId == categoryId;
        }

        private static void ApplyCategoryAliasCounts(IList<CategoryInfo> categories, IList<Product> products)
        {
            foreach (var alias in CategoryAliases)
            {
                var count = products.Count(product => alias.Matches(product));
                var category = categories.FirstOrDefault(item => item.Id == alias.PrimaryCategoryId)
                    ?? categories.FirstOrDefault(item => string.Equals(Normalize(item.Name), Normalize(alias.DisplayName), StringComparison.Ordinal));

                if (category == null)
                {
                    categories.Add(new CategoryInfo
                    {
                        Id = alias.PrimaryCategoryId,
                        Name = alias.DisplayName,
                        Count = count,
                        Slug = Slugify(alias.DisplayName)
                    });
                    continue;
                }

                category.Id = alias.PrimaryCategoryId;
                category.Name = alias.DisplayName;
                category.Count = count;
                category.Slug = Slugify(alias.DisplayName);
            }
        }

        private static CategoryInfo FindSelectedCategory(ProductCatalog catalog, int categoryId)
        {
            var selectedCategory = catalog.Categories.FirstOrDefault(category => category.Id == categoryId);
            if (selectedCategory != null)
            {
                return selectedCategory;
            }

            var alias = FindCategoryAlias(categoryId);
            if (alias == null)
            {
                return null;
            }

            return catalog.Categories.FirstOrDefault(category => category.Id == alias.PrimaryCategoryId)
                ?? catalog.Categories.FirstOrDefault(category => string.Equals(Normalize(category.Name), Normalize(alias.DisplayName), StringComparison.Ordinal));
        }

        private static CategoryAliasRule FindCategoryAlias(int categoryId)
        {
            return CategoryAliases.FirstOrDefault(alias => alias.PublicCategoryIds.Contains(categoryId));
        }

        private static bool MatchesAwardCategoryProduct(Product product)
        {
            if (product == null)
            {
                return false;
            }

            if (MatchesAnyCategoryId(product, 22, 94))
            {
                return true;
            }

            var normalizedName = NormalizeSearchText(product.Name);
            var isAwardNamedProduct = normalizedName.Contains("plaket")
                || normalizedName.Contains("madalya")
                || normalizedName.Contains("odul")
                || normalizedName.Contains("isimlik");

            if (!isAwardNamedProduct)
            {
                return false;
            }

            return product.CategoryId == 33
                || product.SubCategoryId == 33
                || (product.CategoryId == 3 && product.MainCategoryId == 3 && product.SubCategoryId == 0);
        }

        private static bool MatchesAnyCategoryId(Product product, params int[] categoryIds)
        {
            return categoryIds.Contains(product.CategoryId)
                || categoryIds.Contains(product.MainCategoryId)
                || categoryIds.Contains(product.SubCategoryId);
        }

        private static bool MatchesSearch(string value, string normalizedNeedle, IList<string> normalizedNeedleTokens)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(normalizedNeedle))
            {
                return false;
            }

            var normalizedValue = NormalizeSearchText(value);
            if (normalizedValue.Contains(normalizedNeedle))
            {
                return true;
            }

            if (normalizedNeedleTokens == null || normalizedNeedleTokens.Count == 0)
            {
                return false;
            }

            var valueTokens = SearchTokens(value);
            return normalizedNeedleTokens.All(needleToken =>
                valueTokens.Any(valueToken => SearchTokenMatches(valueToken, needleToken)));
        }

        private static bool SearchTokenMatches(string valueToken, string needleToken)
        {
            return string.Equals(valueToken, needleToken, StringComparison.Ordinal)
                || (needleToken.Length >= 3 && valueToken.StartsWith(needleToken, StringComparison.Ordinal))
                || (valueToken.Length >= 3 && needleToken.StartsWith(valueToken, StringComparison.Ordinal))
                || SearchSynonymMatches(valueToken, needleToken);
        }

        private static bool SearchSynonymMatches(string valueToken, string needleToken)
        {
            string[] synonyms;
            switch (needleToken)
            {
                case "bakim":
                    synonyms = new[] { "manikur" };
                    break;
                default:
                    return false;
            }

            return synonyms.Any(synonym =>
                string.Equals(valueToken, synonym, StringComparison.Ordinal)
                || valueToken.StartsWith(synonym, StringComparison.Ordinal)
                || synonym.StartsWith(valueToken, StringComparison.Ordinal));
        }

        private static IList<string> SearchTokens(string text)
        {
            return Regex.Split(NormalizeSearchText(text), @"[^a-z0-9]+")
                .Where(token => token.Length > 1)
                .Select(StemSearchToken)
                .Where(token => token.Length > 1)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static string StemSearchToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length <= 3)
            {
                return token;
            }

            var suffixes = new[]
            {
                "lerden", "lardan", "lerle", "larla", "lerde", "larda", "lere", "lara",
                "leri", "lari", "ler", "lar", "si", "su", "li", "lu", "i", "u"
            };

            foreach (var suffix in suffixes)
            {
                if (token.EndsWith(suffix, StringComparison.Ordinal) && token.Length - suffix.Length >= 3)
                {
                    return token.Substring(0, token.Length - suffix.Length);
                }
            }

            return token;
        }

        private static string NormalizeSearchText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var lowered = text.Trim().ToLower(new CultureInfo("tr-TR"))
                .Replace('\u0131', 'i');
            var decomposed = lowered.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);

            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(character);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.ToLower(new CultureInfo("tr-TR"));
        }

        private static string ShortHash(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return ImageVersionPrefix + "no-image";
            }

            using (var sha1 = SHA1.Create())
            {
                var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(value));
                return ImageVersionPrefix + BitConverter.ToString(bytes).Replace("-", string.Empty).Substring(0, 12).ToLowerInvariant();
            }
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

        private sealed class CategoryAliasRule
        {
            public CategoryAliasRule(string displayName, int primaryCategoryId, int[] publicCategoryIds, Func<Product, bool> matches)
            {
                DisplayName = displayName;
                PrimaryCategoryId = primaryCategoryId;
                PublicCategoryIds = publicCategoryIds;
                Matches = matches;
            }

            public string DisplayName { get; private set; }
            public int PrimaryCategoryId { get; private set; }
            public int[] PublicCategoryIds { get; private set; }
            public Func<Product, bool> Matches { get; private set; }
        }
    }
}
