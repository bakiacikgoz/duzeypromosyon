using System.Collections.Generic;

namespace duzeypromosyonn.Models
{
    public class ProductDetailViewModel
    {
        public Product Product { get; set; }
        public IList<Product> RelatedProducts { get; set; }
    }
}
