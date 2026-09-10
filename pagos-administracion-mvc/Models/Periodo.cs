using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Models
{
    // Periodo de evaluación, jerárquico: un Periodo puede tener un PeriodoPadre (ej. el "1er
    // Parcial" tiene como padre al "1er Trimestre", que a su vez tiene como padre al "1er
    // Cuatrimestre"). Esto evita hardcodear Trimestre1/Trimestre2/Cuatrimestre1 como columnas
    // fijas: si mañana el colegio usa bimestres en vez de trimestres, es la misma estructura,
    // solo cambian los datos, no el modelo.
    //
    // La nota de un Periodo "contenedor" (Trimestral/Cuatrimestral/Anual) sale de promediar las
    // Notas de sus Subperiodos, salvo que el Docente haya cargado una Nota manual para ese
    // Periodo directamente (ver Nota.EsPromedioAutomatico) — que es el caso de "a veces no se
    // coloca la real sino que se tiene en cuenta todo el cuatrimestre".
    public class Periodo
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        public string Nombre { get; set; } = string.Empty; // "1er Parcial", "1er Trimestre", "Nota Final", etc.

        public TipoPeriodo Tipo { get; set; }

        [Display(Name = "Año lectivo")]
        [Range(2020, 2100, ErrorMessage = "Ingresá un año válido.")]
        public int AnioLectivo { get; set; } = DateTime.Today.Year;

        // Null = periodo de nivel superior (ej. un Trimestral sin Cuatrimestral encima, si el
        // colegio no agrupa en cuatrimestres). No es obligatorio completar toda la jerarquía.
        public int? PeriodoPadreId { get; set; }
        [ValidateNever]
        public Periodo? PeriodoPadre { get; set; }

        public ICollection<Periodo> Subperiodos { get; set; } = new List<Periodo>();

        // Soft delete: mismo criterio que el resto del proyecto.
        public bool Activo { get; set; } = true;
    }
}
