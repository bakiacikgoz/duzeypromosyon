using System.ComponentModel.DataAnnotations;

namespace duzeypromosyonn.Models
{
    public class ContactViewModel
    {
        [Required(ErrorMessage = "Ad soyad zorunludur.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Telefon zorunludur.")]
        public string Phone { get; set; }

        [EmailAddress(ErrorMessage = "E-posta adresi geçerli değil.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Mesaj zorunludur.")]
        public string Message { get; set; }
    }
}
