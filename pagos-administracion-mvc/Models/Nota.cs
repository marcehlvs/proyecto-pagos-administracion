using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace pagos_administracion_mvc.Models
{
    public class Nota
    {
        public int Id { get; set; }

        // Mismo ancla que Asistencia: por qué Inscripcion (Alumno+Curso), no por AlumnoId suelto.
        public int InscripcionId { get; set; }
        [ValidateNever]
        public Inscripcion Inscripcion { get; set; } = null!;

        // De qué Asignatura (y de paso, qué Docente la cargó: CursoAsignatura.DocenteUserId).
        public int CursoAsignaturaId { get; set; }
        [ValidateNever]
        public CursoAsignatura CursoAsignatura { get; set; } = null!;

        public int PeriodoId { get; set; }
        [ValidateNever]
        public Periodo Periodo { get; set; } = null!;

        [Range(0, 10, ErrorMessage = "La nota debe estar entre 0 y 10.")]
        public decimal Valor { get; set; }

        // true = Valor sale de promediar las Notas de los Subperiodos de este Periodo (se
        // recalcula solo). false = el Docente cargó este Valor a mano y gana por encima de
        // cualquier promedio, aunque haya Notas cargadas en los Subperiodos — es el caso de
        // "a veces no se coloca la real sino que se tiene en cuenta todo el cuatrimestre".
        [Display(Name = "Es promedio automático")]
        public bool EsPromedioAutomatico { get; set; } = true;

        [Display(Name = "Observación")]
        public string? Observacion { get; set; }

        // Soft delete: mismo criterio que el resto del proyecto.
        public bool Activo { get; set; } = true;

        // Auditoría de creación/edición, mismo patrón que Asistencia/Cuota.
        public string? CargadaPorNombre { get; set; }
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public string? ModificadaPorNombre { get; set; }
        public DateTime? FechaModificacion { get; set; }
    }
}
