using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace pagos_administracion_mvc.Models
{
    // Un examen de múltiple choice creado por el Docente para una materia/curso.
    // Mismo patrón que Tarea: usa CursoAsignatura como pivot.
    public class Examen
    {
        public int Id { get; set; }

        public int CursoAsignaturaId { get; set; }
        [ValidateNever]
        public CursoAsignatura CursoAsignatura { get; set; } = null!;

        [Required(ErrorMessage = "El título es obligatorio.")]
        [Display(Name = "Título")]
        [StringLength(120)]
        public string Titulo { get; set; } = string.Empty;

        [Display(Name = "Descripción / Instrucciones")]
        public string? Descripcion { get; set; }

        // Ventana de disponibilidad: el alumno solo puede rendir entre estas fechas.
        [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
        [Display(Name = "Disponible desde")]
        public DateTime FechaDesde { get; set; }

        [Required(ErrorMessage = "La fecha de cierre es obligatoria.")]
        [Display(Name = "Disponible hasta")]
        public DateTime FechaHasta { get; set; }

        // Tiempo máximo para completar el examen en minutos.
        // Null = sin límite de tiempo.
        [Display(Name = "Tiempo límite (minutos)")]
        [Range(1, 300, ErrorMessage = "El tiempo debe estar entre 1 y 300 minutos.")]
        public int? TiempoLimiteMinutos { get; set; }

        // Nota aprobatoria mínima (0–10). Null = sin nota mínima definida.
        [Display(Name = "Nota mínima aprobatoria")]
        [Range(1, 10, ErrorMessage = "La nota mínima debe estar entre 1 y 10.")]
        public decimal? NotaMinimaAprobatoria { get; set; }

        // Si true, el alumno solo puede rendir una vez. Si false, puede reintentar.
        [Display(Name = "Solo un intento")]
        public bool SoloUnIntento { get; set; } = true;

        // Si true, las preguntas se muestran en orden aleatorio.
        [Display(Name = "Orden aleatorio de preguntas")]
        public bool OrdenAleatorio { get; set; } = false;

        // Si true, el alumno ve el resultado (nota y respuestas correctas) al terminar.
        [Display(Name = "Mostrar resultado al terminar")]
        public bool MostrarResultado { get; set; } = true;

        // Vinculación opcional a un Periodo para que la nota impacte en el boletín.
        // Mismo criterio que Tarea.PeriodoId.
        [Display(Name = "Período")]
        public int? PeriodoId { get; set; }
        [ValidateNever]
        public Periodo? Periodo { get; set; }

        // Soft delete: mismo criterio que el resto del proyecto.
        public bool Activo { get; set; } = true;

        // Auditoría.
        public string? CreadoPorNombre { get; set; }
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public ICollection<PreguntaExamen> Preguntas { get; set; } = new List<PreguntaExamen>();
        public ICollection<IntentoExamen> Intentos { get; set; } = new List<IntentoExamen>();
    }
}
