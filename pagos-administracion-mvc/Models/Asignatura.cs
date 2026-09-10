using System.ComponentModel.DataAnnotations;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Models
{
    // Catálogo de materias curriculares (Matemática, Lengua, etc.), distinto del enum Materia
    // que ya existe (Clase/EducacionFisica) porque ese enum solo distingue el tipo de registro
    // de Asistencia, no la materia real que se dicta. No se tocan ni se renombran para no romper
    // Asistencia: son dos conceptos separados a propósito.
    public class Asignatura
    {
        public int Id { get; set; }

        [Required, StringLength(80)]
        public string Nombre { get; set; } = string.Empty;

        // De qué nivel es esta asignatura. Si algún día una materia aplica a los dos niveles con
        // el mismo nombre, se carga un registro por Nivel (mismo criterio simple que ArancelNivel:
        // no se comparte fila entre niveles), así CursoAsignatura siempre filtra por Nivel del Curso.
        public NivelEducativo Nivel { get; set; }

        // Soft delete: mismo criterio que el resto del proyecto.
        public bool Activo { get; set; } = true;

        public string? CreadaPorNombre { get; set; }
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public string? ModificadaPorNombre { get; set; }
        public DateTime? FechaModificacion { get; set; }
    }
}
