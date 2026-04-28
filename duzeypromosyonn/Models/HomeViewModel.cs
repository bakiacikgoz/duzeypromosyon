using System.Collections.Generic;

namespace duzeypromosyonn.Models
{
    public class HomeViewModel
    {
        public IList<Slider> Sliders { get; set; }
        public IList<Product> FeaturedProducts { get; set; }
        public IList<CategoryInfo> Categories { get; set; }
        public int TotalProducts { get; set; }
        public XmlUpdateStatus XmlStatus { get; set; }
    }
}
