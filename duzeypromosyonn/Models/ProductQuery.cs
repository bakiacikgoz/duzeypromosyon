namespace duzeypromosyonn.Models
{
    public class ProductQuery
    {
        public string Q { get; set; }
        public int? CategoryId { get; set; }
        public string AltKategori { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public bool? InStock { get; set; }
        public string Color { get; set; }
        public string Sort { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }

        public int EffectivePage
        {
            get { return Page <= 0 ? 1 : Page; }
        }

        public int EffectivePageSize
        {
            get
            {
                if (PageSize <= 0) return 24;
                if (PageSize > 36) return 36;
                return PageSize;
            }
        }
    }
}
