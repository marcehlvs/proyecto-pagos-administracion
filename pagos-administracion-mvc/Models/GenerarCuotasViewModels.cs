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

        [Range(2020, 2100)]
        public int Anio { get; set; } = DateTime.Today.Year;

        [Range(0.01, double.MaxValue)]
        public decimal MontoBase { get; set; }

        [DataType(DataType.Date)]
        public DateTime FechaVencimiento { get; set; } = DateTime.Today.AddDays(30);

        [Range(0, 100)]
        public decimal DescuentoHermanoPorcentaje { get; set; } = 10;

        [Range(0, 100)]
        public decimal DescuentoPagoATiempoPorcentaje { get; set; } = 5;

        [Range(0, 30)]
        public int DiasParaDescuento { get; set; } = 10;
    }
}