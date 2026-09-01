using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace pagos_administracion_mvc.Models
{
    public class Inscripcion
    {
        public int Id { get; set; }

        public int AlumnoId { get; set; }
        [ValidateNever]
        public Alumno Alumno { get; set; } = null!;

        public int CursoId { get; set; }
        [ValidateNever]
        public Curso Curso { get; set; } = null!;

        public DateTime FechaInscripcion { get; set; } = DateTime.Now;

        // Soft delete: mismo criterio que el resto del proyecto. Una inscripción con asistencias
        // ya cargadas no se borra físicamente (Asistencia -> Inscripcion es Restrict a propósito).
        public bool Activo { get; set; } = true;

        public ICollection<Asistencia> Asistencias { get; set; } = new List<Asistencia>();
    }
}
