using System.ComponentModel.DataAnnotations;

namespace pagos_administracion_mvc.Models
{
    public class FamiliaCreateViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        // La contraseña provisoria ya no la tipea el admin: se genera random en el
        // controller (ver FamiliasController.Create) y se manda por mail a la familia.

        public List<int> AlumnoIds { get; set; } = new();
    }
}