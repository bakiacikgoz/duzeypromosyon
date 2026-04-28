using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace duzeypromosyonn.Models
{
    public class Canta
    {
        public Canta()
        {
            ImageUrls = new List<string>();
        }

        public int Id { get; set; }

        [Required(ErrorMessage = "Başlık zorunludur.")]
        public string Baslik { get; set; }

        [Required(ErrorMessage = "Ürün kodu zorunludur.")]
        public string UrunKodu { get; set; }

        [Range(0, 9999999, ErrorMessage = "Fiyat geçerli olmalıdır.")]
        public decimal Fiyat { get; set; }

        [AllowHtml]
        public string Aciklama { get; set; }

        public List<string> ImageUrls { get; set; }
    }
}
