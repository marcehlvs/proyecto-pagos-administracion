using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using pagos_administracion_mvc.Data;

namespace pagos_administracion_mvc.Models
{
    // Representa una sesión de rendición de examen por parte de un Alumno (vía Inscripcion).
    // Si SoloUnIntento = false, puede haber múltiples IntentoExamen para el mismo Examen+Inscripcion.
    public class IntentoExamen
    {
        public int Id { get; set; }

        public int ExamenId { get; set; }
        [ValidateNever]
        public Examen Examen { get; set; } = null!;

        public int InscripcionId { get; set; }
        [ValidateNever]
        public Inscripcion Inscripcion { get; set; } = null!;

        // Momento en que el alumno comenzó el intento.
        public DateTime FechaInicio { get; set; } = DateTime.Now;

        // Momento en que el alumno envió/finalizó el examen. Null = todavía en curso.
        public DateTime? FechaFin { get; set; }

        // Nota calculada al finalizar (0–10). Null si todavía no terminó.
        public decimal? NotaValor { get; set; }

        // true = aprobado (NotaValor >= Examen.NotaMinimaAprobatoria, o simplemente terminado si no
        // hay nota mínima definida). Null = aún no completado.
        public bool? Aprobado { get; set; }

        // Puntaje bruto obtenido (suma de puntajes de preguntas correctas).
        public int PuntajeObtenido { get; set; } = 0;

        // Puntaje total posible (suma de puntajes de todas las preguntas).
        public int PuntajeTotal { get; set; } = 0;

        // Vinculación opcional a la Nota de boletín generada automáticamente al finalizar.
        // Mismo patrón que Entrega.NotaId.
        public int? NotaId { get; set; }
        [ValidateNever]
        public Nota? NotaBoletín { get; set; }

        public ICollection<IntentoPregunta> Respuestas { get; set; } = new List<IntentoPregunta>();
    }
}
