using System.ComponentModel.DataAnnotations;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Models
{
    public class GenerarCuotasViewModel
    {
        public NivelEducativo? Nivel { get; set; }
        public Turno? Turno { get; set; }

        [Range(1, 12)]
        public int Mes { get; set; } = DateTime.Today.Month;
        [Display(Name ="Año")]
        [Range(2020, 2100)]
        public int Anio { get; set; } = DateTime.Today.Year;

        // Ya no se carga a mano: se toma del ArancelNivel vigente para el Nivel de cada alumno
        // (ver ArancelesController). Si un alumno no tiene arancel vigente cargado para su Nivel,
        // se omite y se informa en el resultado.

        [DataType(DataType.Date)]
        public DateTime FechaVencimiento { get; set; } = DateTime.Today.AddDays(30);

        [Range(0, 100)]
        public decimal DescuentoHermanoPorcentaje { get; set; } = 10;

        // Descuento % adicional y OPCIONAL sobre el pago a tiempo. La bonificación fija de la
        // circular (ArancelNivel.BonificacionPagoATiempo) ya se aplica siempre que exista; este
        // % es para sumar un descuento propio de la institución encima, si hiciera falta. Por eso
        // arranca en 0 y no en 5 como antes.
        [Range(0, 100)]
        public decimal DescuentoPagoATiempoPorcentaje { get; set; } = 0;

        [Range(0, 30)]
        public int DiasParaDescuento { get; set; } = 10;
    }
}