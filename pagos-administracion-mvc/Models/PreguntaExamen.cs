using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace pagos_administracion_mvc.Models
{
    // Una pregunta de un Examen. Puede tener 2–6 opciones, exactamente una marcada como correcta.
    public class PreguntaExamen
    {
        public int Id { get; set; }

        public int ExamenId { get; set; }
        [ValidateNever]
        public Examen Examen { get; set; } = null!;

        [Required(ErrorMessage = "El enunciado es obligatorio.")]
        [Display(Name = "Enunciado")]
        [StringLength(600)]
        public string Enunciado { get; set; } = string.Empty;

        // Orden de presentación (1, 2, 3, …). Con OrdenAleatorio = true se ignora en la vista,
        // pero se conserva para identificar la posición original al corregir.
        [Display(Name = "Orden")]
        public int Orden { get; set; }

        // Puntos que vale esta pregunta para el cálculo final de nota.
        // Por defecto 1; permite preguntas con diferente peso dentro del mismo examen.
        [Display(Name = "Puntaje")]
        [Range(1, 100)]
        public int Puntaje { get; set; } = 1;

        // Soft delete encadenado al Examen padre.
        public bool Activo { get; set; } = true;

        public ICollection<OpcionRespuesta> Opciones { get; set; } = new List<OpcionRespuesta>();
        public ICollection<IntentoPregunta> IntentosPreguntas { get; set; } = new List<IntentoPregunta>();
    }
}
