using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Models
{
    public class Asistencia
    {
        public int Id { get; set; }

        public int InscripcionId { get; set; }
        [ValidateNever]
        public Inscripcion Inscripcion { get; set; } = null!;

        [DataType(DataType.Date)]
        public DateTime Fecha { get; set; }

        // Distingue si este registro corresponde a la clase regular o a Educación Física.
        // Un mismo día puede tener hasta dos registros (uno por Materia) para la misma Inscripcion.
        public Materia Materia { get; set; } = Materia.Clase;

        public EstadoAsistencia Estado { get; set; } = EstadoAsistencia.Presente;

        [Display(Name = "Observación")]
        public string? Observacion { get; set; }

        // Soft delete: mismo criterio que el resto del proyecto. No se borra físicamente,
        // se da de baja para no perder historial.
        public bool Activo { get; set; } = true;

        // Auditoría de creación/edición, mismo patrón que Cuota.
        public string? CreadaPorNombre { get; set; }
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public string? ModificadaPorNombre { get; set; }
        public DateTime? FechaModificacion { get; set; }
    }
}
