using System;
using System.Collections.Generic;

namespace duzeypromosyonn.Models
{
    public class ProductCatalog
    {
        public ProductCatalog()
        {
            Products = new List<Product>();
            Categories = new List<CategoryInfo>();
            SubCategories = new List<string>();
            Colors = new List<string>();
        }

        public IList<Product> Products { get; set; }
        public IList<CategoryInfo> Categories { get; set; }
        public IList<string> SubCategories { get; set; }
        public IList<string> Colors { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public DateTime? LastXmlWriteTime { get; set; }
    }
}
