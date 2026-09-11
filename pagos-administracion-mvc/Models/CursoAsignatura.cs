using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using pagos_administracion_mvc.Data;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Models
{
    // Une un Curso con una Asignatura que se dicta ahí, y con el Docente que la dicta. Esto es lo
    // que resuelve "varios Docentes por Curso": cada Asignatura de un Curso tiene su propio
    // Docente, en vez de depender del Curso.ProfesorUserId único (que sigue existiendo sin
    // cambios, como el Docente/preceptor a cargo general de Asistencia).
    public class CursoAsignatura
    {
        public int Id { get; set; }

        public int CursoId { get; set; }
        [ValidateNever]
        public Curso Curso { get; set; } = null!;

        public int AsignaturaId { get; set; }
        [ValidateNever]
        public Asignatura Asignatura { get; set; } = null!;

        // Nullable a propósito: el Docente de esta Asignatura en este Curso puede no estar
        // asignado todavía (el admin arma el Curso eligiendo materias primero, y asigna el
        // Docente de cada una después, igual que Curso.ProfesorUserId ya es nullable).
        public string? DocenteUserId { get; set; }
        [ValidateNever]
        public ApplicationUser? DocenteUser { get; set; }

        // Nullable: si no se especifica, esta Asignatura hereda el Turno del Curso (Curso.Turno).
        // Solo se completa cuando la materia se dicta en el turno contrario al del Curso (ej. un
        // taller optativo de la tarde dentro de un Curso de turno Mañana).
        public Turno? TurnoOverride { get; set; }

        // Soft delete: mismo criterio que el resto del proyecto. Con Notas cargadas, la relación
        // Curso-Asignatura-Docente no se borra físicamente (ver Nota -> CursoAsignatura, Restrict).
        public bool Activo { get; set; } = true;

        public ICollection<Nota> Notas { get; set; } = new List<Nota>();
    }
}
