using System.Collections.Generic;

namespace duzeypromosyonn.Models
{
    public class Product
    {
        public Product()
        {
            Options = new List<ProductOption>();
        }

        public int Id { get; set; }
        public int CategoryId { get; set; }
        public int MainCategoryId { get; set; }
        public int SubCategoryId { get; set; }
        public string GroupCode { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public string ImageVersion { get; set; }
        public string IntroHtml { get; set; }
        public string DescriptionHtml { get; set; }
        public string CategoryMain { get; set; }
        public string CategorySub { get; set; }
        public string Unit { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; }
        public bool VatIncluded { get; set; }
        public int VatRate { get; set; }
        public int TotalStock { get; set; }
        public string SupplierCode { get; set; }
        public string SourceUrl { get; set; }
        public IList<ProductOption> Options { get; set; }
    }
}
