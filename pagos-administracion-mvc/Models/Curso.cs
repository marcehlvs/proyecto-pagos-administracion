using System.ComponentModel.DataAnnotations;
using pagos_administracion_mvc.Data;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Models
{
    public class Curso
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        public NivelEducativo Nivel { get; set; }

        [Display(Name = "Grado-Año")]
        [Range(1, 6, ErrorMessage = "El grado debe estar entre 1 y 6.")]
        public int GradoAnio { get; set; }

        public Turno Turno { get; set; }

        // Días de Educación Física de este curso en particular (configurable, puede variar
        // entre cursos). Ej: Lunes | Viernes. Ninguno = el curso no tiene EF registrada.
        [Display(Name = "Días de Educación Física")]
        public DiasSemana DiasEducacionFisica { get; set; } = DiasSemana.Ninguno;

        // Docente a cargo de tomar asistencia de este curso. Nullable: el Admin siempre puede
        // tomar asistencia de cualquier curso, tenga o no un Docente asignado.
        public string? ProfesorUserId { get; set; }
        public ApplicationUser? ProfesorUser { get; set; }

        // Soft delete: mismo criterio que Alumno/Cuota/Pago. Un curso con inscripciones o
        // asistencias cargadas no se borra físicamente, así que "eliminar" en la UI da de baja.
        public bool Activo { get; set; } = true;

        public ICollection<Inscripcion> Inscripciones { get; set; } = new List<Inscripcion>();

        // Determina si una fecha puntual corresponde a un día de Educación Física de este curso.
        public bool TieneEducacionFisica(DateTime fecha)
        {
            var diaDelFlag = fecha.DayOfWeek switch
            {
                DayOfWeek.Monday => DiasSemana.Lunes,
                DayOfWeek.Tuesday => DiasSemana.Martes,
                DayOfWeek.Wednesday => DiasSemana.Miercoles,
                DayOfWeek.Thursday => DiasSemana.Jueves,
                DayOfWeek.Friday => DiasSemana.Viernes,
                DayOfWeek.Saturday => DiasSemana.Sabado,
                _ => DiasSemana.Ninguno
            };

            return diaDelFlag != DiasSemana.Ninguno && DiasEducacionFisica.HasFlag(diaDelFlag);
        }
    }
}
