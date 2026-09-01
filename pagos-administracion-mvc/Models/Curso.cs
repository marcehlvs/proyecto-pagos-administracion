using System.ComponentModel.DataAnnotations;
using pagos_administracion_mvc.Data;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Models
{
    public class Curso
    {
        public int Id { get; set; }

        // Opcional: para la mayoría de los cursos alcanza con Nivel+GradoAnio+Turno (ver
        // Etiqueta más abajo). Sirve para casos como "Proyecto" o "Sección A" donde el admin
        // quiere un nombre propio además de esos datos.
        // string? (nullable) a propósito: con Nullable Reference Types habilitado, ASP.NET Core
        // agrega un [Required] IMPLÍCITO a cualquier string no-nullable, así que aunque sacamos
        // el [Required] de acá, el binding lo seguía pidiendo. Marcándolo "?" se lo desactivamos.
        // La columna en la DB sigue siendo NOT NULL (ver AdministracionDbContext, Property(...).IsRequired()),
        // así que no hace falta una migración nueva; el controller garantiza que nunca se guarde null.
        [Display(Name = "Nombre (opcional)")]
        public string? Nombre { get; set; } = string.Empty;

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

        // Para mostrar en listados/títulos donde antes se usaba Nombre a secas. Si el admin
        // puso un nombre propio lo respeta; si no, arma uno a partir de Nivel+GradoAnio+Turno
        // (que ya son obligatorios), para que un curso sin Nombre nunca se vea en blanco.
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string Etiqueta => string.IsNullOrWhiteSpace(Nombre)
            ? $"{Nivel} {GradoAnio}° - Turno {Turno}"
            : Nombre;

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
