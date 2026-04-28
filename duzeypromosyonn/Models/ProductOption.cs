namespace duzeypromosyonn.Models
{
    public class ProductOption
    {
        public int VariationId { get; set; }
        public string StockCode { get; set; }
        public int StockQuantity { get; set; }
        public decimal Price { get; set; }
        public string Color { get; set; }
    }
}
