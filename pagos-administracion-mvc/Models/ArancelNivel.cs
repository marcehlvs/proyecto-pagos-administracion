using System.ComponentModel.DataAnnotations;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Models
{
    // Valores de arancel por Nivel (Primaria/Secundaria), desglosados en los mismos conceptos
    // que publica la Comisión de Aranceles en su circular mensual: Curricular, Extracurricular,
    // Equipamiento Didáctico, Mantenimiento y Emergencia Médica. Se guarda un registro por
    // Nivel + VigenteDesde (no se pisa el anterior) para poder actualizar los montos cuando
    // cambian sin perder el histórico de lo que se cobró en meses anteriores.
    public class ArancelNivel
    {
        public int Id { get; set; }

        public NivelEducativo Nivel { get; set; }

        // Fecha a partir de la cual rige este arancel. GenerarCuotasController toma, para cada
        // Nivel, el registro Activo con VigenteDesde más reciente que sea <= hoy.
        [Display(Name = "Vigente desde")]
        [DataType(DataType.Date)]
        public DateTime VigenteDesde { get; set; } = DateTime.Today;

        [Range(0, double.MaxValue, ErrorMessage = "El monto no puede ser negativo.")]
        public decimal Curricular { get; set; }

        [Display(Name = "Extra curricular")]
        [Range(0, double.MaxValue, ErrorMessage = "El monto no puede ser negativo.")]
        public decimal ExtraCurricular { get; set; }

        [Display(Name = "Equipamiento didáctico")]
        [Range(0, double.MaxValue, ErrorMessage = "El monto no puede ser negativo.")]
        public decimal EquipamientoDidactico { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "El monto no puede ser negativo.")]
        public decimal Mantenimiento { get; set; }

        [Display(Name = "Emergencia médica")]
        [Range(0, double.MaxValue, ErrorMessage = "El monto no puede ser negativo.")]
        public decimal EmergenciaMedica { get; set; }

        // Bonificación fija por pago dentro de término (ej. del 1 al 12 de cada mes, según la
        // circular). Es un MONTO FIJO, no un porcentaje: se resta directo del total, aparte del
        // GenerarCuotasViewModel.DescuentoPagoATiempoPorcentaje (que sigue existiendo como
        // descuento % opcional adicional).
        [Display(Name = "Bonificación por pago a tiempo (monto fijo)")]
        [Range(0, double.MaxValue, ErrorMessage = "El monto no puede ser negativo.")]
        public decimal BonificacionPagoATiempo { get; set; }

        // Suma de los 5 conceptos, antes de la bonificación ("Cuota real" en la circular).
        public decimal CuotaReal => Curricular + ExtraCurricular + EquipamientoDidactico + Mantenimiento + EmergenciaMedica;

        // Total a pagar dentro de término ("Cuota real" - Bonificación en la circular).
        public decimal TotalConBonificacion => CuotaReal - BonificacionPagoATiempo;

        // Soft delete: mismo criterio que el resto del sistema (Cuota, Alumno, Pago, Curso).
        public bool Activo { get; set; } = true;

        public string? CreadaPorNombre { get; set; }
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public string? ModificadaPorNombre { get; set; }
        public DateTime? FechaModificacion { get; set; }
    }
}
