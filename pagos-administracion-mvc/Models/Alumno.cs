using pagos_administracion_mvc.Data;
using System.ComponentModel.DataAnnotations;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Models
{
    public class Alumno
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;
        public NivelEducativo Nivel { get; set; }
        [Display(Name = "Grado-Año")]
        [Range(1, 6, ErrorMessage = "El grado debe estar entre 1 y 6.")]
        public int GradoAnio { get; set; }
        public Turno Turno { get; set; }

        // TIPO del RITE (C = cursa por primera vez / R = recursa el año): lo carga el Admin
        // sobre el Alumno, no por materia — en este colegio, si un alumno recursa, recursa el
        // año completo (no hay recursado materia por materia todavía). Se imprime igual en
        // todas las filas de materia del boletín RITE.
        [Display(Name = "Recursa este año")]
        public bool EsRecursante { get; set; } = false;

        public string? FamiliaUserId { get; set; }
        public ApplicationUser? FamiliaUser { get; set; }

        // Login propio del alumno (rol "Alumno"), independiente del login de la Familia.
        // Alta independiente por ahora: no requiere que el alumno tenga FamiliaUserId cargado.
        public string? AlumnoUserId { get; set; }
        public ApplicationUser? AlumnoUser { get; set; }

        //Un alumno puede estar inscripto en varios cursos
        public ICollection<Inscripcion> Inscripciones { get; set; } = new List<Inscripcion>();

        // Soft delete: mismo criterio que Pago/Cuota. Un alumno con cuotas cargadas no se puede
        // borrar físicamente (Cuota -> Alumno es Restrict a propósito, para no perder historial de
        // pagos), así que "eliminar" en la UI da de baja en vez de borrar la fila.
        public bool Activo { get; set; } = true;

        //Un alumno tiene muchas cuotas (una por mes)
        public ICollection<Cuota> Cuotas { get; set; } = new List<Cuota>();
    }
}