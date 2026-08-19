using System.ComponentModel.DataAnnotations;

namespace pagos_administracion_mvc.Models
{
    public class FamiliaCreateViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public List<int> AlumnoIds { get; set; } = new();
    }
}