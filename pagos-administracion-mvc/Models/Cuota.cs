using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Models
{
    public class Cuota
    {
        public int Id { get; set; }
        public int AlumnoId { get; set; }

        // Mismo caso que Pago.Cuota: no viene en el [Bind] de CuotasController, así que
        // sin [ValidateNever] queda null y hace fallar ModelState.IsValid en silencio.
        [ValidateNever]
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
        public ICollection<ContactoManual> ContactosManuales { get; set; } = new List<ContactoManual>();

        // Requieren que la colección Pagos esté cargada (.Include(c => c.Pagos)) para dar un valor
        // real; si no está cargada, TotalPagado da 0 y SaldoPendiente cae al total (comportamiento
        // seguro por defecto: nunca subestima lo que falta pagar).
        public decimal TotalPagado => Pagos.Where(p => p.Estado == EstadoPago.Aprobado).Sum(p => p.Monto);
        public decimal SaldoPendiente => MontoAPagar - TotalPagado;

        // Soft delete: mismo criterio que en Pago, nunca se borra una cuota físicamente.
        public bool Activo { get; set; } = true;

        // Auditoría de creación/edición.
        public string? CreadaPorNombre { get; set; }
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public string? ModificadaPorNombre { get; set; }
        public DateTime? FechaModificacion { get; set; }

        // Idempotencia de los avisos por mail de RevisorVencimientosService: cada aviso se manda
        // una única vez por cuota, sin importar cuántas veces corra el job en el día ni si el Estado
        // oscila (ej. Vencida -> Parcial por un pago parcial -> Vencida de nuevo).
        public bool AvisoProximoVencimientoEnviado { get; set; } = false;
        public bool AvisoVencidaEnviado { get; set; } = false;
    }
}
