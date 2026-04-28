using System.Collections.Generic;

namespace duzeypromosyonn.Models
{
    public class ProductSearchResult
    {
        public ProductSearchResult()
        {
            Items = new List<Product>();
            Categories = new List<CategoryInfo>();
            SubCategories = new List<string>();
            Colors = new List<string>();
            Query = new ProductQuery();
        }

        public IList<Product> Items { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public ProductQuery Query { get; set; }
        public IList<CategoryInfo> Categories { get; set; }
        public IList<string> SubCategories { get; set; }
        public IList<string> Colors { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
    }
}
