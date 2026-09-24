using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace pagos_administracion_mvc.Models
{
    // Una opción de respuesta dentro de una PreguntaExamen.
    // Una y solo una opción por pregunta tiene EsCorrecta = true.
    public class OpcionRespuesta
    {
        public int Id { get; set; }

        public int PreguntaExamenId { get; set; }
        [ValidateNever]
        public PreguntaExamen PreguntaExamen { get; set; } = null!;

        [Required(ErrorMessage = "El texto de la opción es obligatorio.")]
        [Display(Name = "Texto")]
        [StringLength(300)]
        public string Texto { get; set; } = string.Empty;

        [Display(Name = "Es correcta")]
        public bool EsCorrecta { get; set; } = false;

        // Letra identificadora: A, B, C, D… Se genera automáticamente al guardar.
        [StringLength(1)]
        public string Letra { get; set; } = string.Empty;
    }
}
