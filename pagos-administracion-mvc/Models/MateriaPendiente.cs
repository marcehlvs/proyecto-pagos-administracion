using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace pagos_administracion_mvc.Models
{
    // Materia pendiente de aprobación de un año anterior (RITE, Fase 3): el alumno adeuda una
    // Asignatura de un GradoAnio que ya cursó (ej. "Matemática 3°" mientras ya está en 4°). No
    // depende de CursoAsignatura ni de ningún año lectivo en curso — el alumno puede ni siquiera
    // tener esa Asignatura entre sus materias actuales — por eso es un registro aparte, propio
    // de Alumno, no de Inscripcion. Lo cargan Admin y Preceptor (ver MateriasPendientesController),
    // no el Docente: es un dato administrativo de arrastre, no una calificación del ciclo.
    public class MateriaPendiente
    {
        public int Id { get; set; }

        public int AlumnoId { get; set; }
        [ValidateNever]
        public Alumno Alumno { get; set; } = null!;

        // La materia se elige del catálogo existente (Asignatura), no como texto libre: el
        // catálogo no tiene GradoAnio propio (una Asignatura es la misma fila para todos los
        // años de un Nivel), así que el año que se adeuda se carga acá aparte.
        public int AsignaturaId { get; set; }
        [ValidateNever]
        public Asignatura Asignatura { get; set; } = null!;

        // Año que adeuda (ej. 3, aunque el alumno ya esté cursando 4°) — junto con Asignatura
        // arma la etiqueta que pide el RITE, ej. "Matemática 3°" (ver Etiqueta).
        [Display(Name = "Año que adeuda")]
        [Range(1, 7, ErrorMessage = "Ingresá un año válido.")]
        public int GradoAnio { get; set; }

        [Display(Name = "Aprobada")]
        public bool Aprobada { get; set; } = false;

        [Display(Name = "Fecha de aprobación")]
        [DataType(DataType.Date)]
        public DateTime? FechaAprobacion { get; set; }

        [Display(Name = "Observación")]
        public string? Observacion { get; set; }

        // "Matemática 3°": lo que se imprime en el RITE y se muestra en los listados. No se
        // guarda en la base (se arma siempre desde Asignatura+GradoAnio), así que si algún día
        // se renombra la Asignatura, esto se actualiza solo.
        public string Etiqueta => $"{Asignatura?.Nombre} {GradoAnio}°";

        // Soft delete: mismo criterio que el resto del proyecto.
        public bool Activo { get; set; } = true;

        // Auditoría de creación/edición, mismo patrón que Nota/Asistencia/Cuota. Admin y
        // Preceptor pueden cargar esto (ver MateriasPendientesController), por eso el nombre
        // guardado puede ser de cualquiera de los dos roles.
        public string? CargadaPorNombre { get; set; }
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public string? ModificadaPorNombre { get; set; }
        public DateTime? FechaModificacion { get; set; }
    }
}
