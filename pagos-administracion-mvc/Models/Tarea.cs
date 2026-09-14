using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace pagos_administracion_mvc.Models
{
    // Una tarea/actividad asignada por el Docente a todo el Curso de una materia (mismo pivot
    // que Nota: CursoAsignatura ya resuelve "de qué materia, en qué Curso, con qué Docente").
    public class Tarea
    {
        public int Id { get; set; }

        public int CursoAsignaturaId { get; set; }
        [ValidateNever]
        public CursoAsignatura CursoAsignatura { get; set; } = null!;

        [Required(ErrorMessage = "El título es obligatorio.")]
        [Display(Name = "Título")]
        public string Titulo { get; set; } = string.Empty;

        [Display(Name = "Consigna")]
        public string? Descripcion { get; set; }

        [Display(Name = "Fecha de entrega")]
        public DateTime FechaEntrega { get; set; }

        // Nullable a propósito: si el Docente elige un Periodo acá, calificar una Entrega de
        // esta Tarea crea/actualiza una Nota suelta (Orden > 0) dentro de ese Periodo — se
        // promedia sola con el resto de las notas del Periodo (ver NotaCalculadora, que ya
        // promedia cualquier nota suelta cargada ahí, sin cambios). Si queda en null, la
        // calificación de la Tarea es informativa nomás y no impacta en el boletín.
        public int? PeriodoId { get; set; }
        [ValidateNever]
        public Periodo? Periodo { get; set; }

        // Soft delete: mismo criterio que el resto del proyecto. Con Entregas ya cargadas, la
        // Tarea no se borra físicamente (Entrega -> Tarea es Restrict a propósito).
        public bool Activo { get; set; } = true;

        // Auditoría, mismo patrón que Nota/Asistencia.
        public string? CreadaPorNombre { get; set; }
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public ICollection<Entrega> Entregas { get; set; } = new List<Entrega>();
    }
}
