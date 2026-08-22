using System.ComponentModel.DataAnnotations;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Models
{
    public class Cuota
    {
        public int Id { get; set; }
        public int AlumnoId { get; set; }
        public Alumno Alumno { get; set; } = null!;
        [Range(1, 12, ErrorMessage = "El mes debe estar entre 1 y 12.")]
        public int Mes { get; set; }
        [Range(2020, 2100, ErrorMessage = "Ingresá un año válido.")]
        [Display(Name ="Año")]
        public int Anio { get; set; }
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0.")]

        public decimal Monto { get; set; }
        [DataType(DataType.Date)]

        public DateTime FechaVencimiento { get; set; }
        public EstadoCuota Estado { get; set; } = EstadoCuota.Pendiente;
        //Una cuota puede tener varios pagos parciales o reintentos

        public decimal? MontoConDescuento { get; set; }
        public DateTime? FechaLimiteDescuento { get; set; }
        public decimal MontoAPagar =>
            (MontoConDescuento.HasValue && FechaLimiteDescuento.HasValue && DateTime.Today <= FechaLimiteDescuento.Value)
            ? MontoConDescuento.Value
            : Monto;
        public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    }
}
