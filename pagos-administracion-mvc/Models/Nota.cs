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

        // 0 = el valor "consolidado" de este Periodo para el boletín: si el Periodo tiene
        // Subperiodos, sale de promediarlos; si no (un Trimestre/Cuatrimestre sin Subperiodos
        // creados), sale de promediar las notas sueltas (Orden 1, 2, 3...) que el Docente fue
        // cargando ahí mismo. En cualquier caso, si el Docente carga esta fila (Orden 0) a mano,
        // ese valor gana siempre (ver EsPromedioAutomatico).
        //
        // 1, 2, 3... = una nota suelta dentro del Periodo (ej. el "3er parcial de Matemática del
        // 1er Trimestre"). El Docente puede cargar tantas como quiera: no hace falta que el Admin
        // cree un Periodo por cada una.
        public int Orden { get; set; } = 0;

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

        // Valoración preliminar del RITE (TEA/TEP/TED), solo tiene sentido en la fila Orden = 0
        // de un Periodo Tipo = Cuatrimestral: es una apreciación aparte de la calificación
        // numérica, la carga el Docente una vez por cuatrimestre (ver NotasController.Cargar,
        // columna "Valoración preliminar" solo visible para Periodos Cuatrimestrales).
        [Display(Name = "Valoración preliminar")]
        public Enums.ValoracionPreliminar? ValoracionPreliminar { get; set; }

        // Soft delete: mismo criterio que el resto del proyecto.
        public bool Activo { get; set; } = true;

        // Auditoría de creación/edición, mismo patrón que Asistencia/Cuota.
        public string? CargadaPorNombre { get; set; }
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public string? ModificadaPorNombre { get; set; }
        public DateTime? FechaModificacion { get; set; }
    }
}
