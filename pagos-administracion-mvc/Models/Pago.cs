using System.ComponentModel.DataAnnotations;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Models
{
    public class Pago
    {
        public int Id { get; set; }
        public int CuotaId { get; set; }
        public Cuota Cuota { get; set; } = null!;
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0.")]

        public decimal Monto { get; set; }
        [DataType(DataType.Date)]
        public DateTime Fecha {  get; set; }
        public EstadoPago Estado { get; set; }

        public string MercadoPagoPaymentId { get; set; } = string.Empty;
        public string MercadoPagoPreferenceId { get; set; } = string.Empty;
        public string? ComprobanteRuta { get; set; }

        // Soft delete: nunca se borra un pago de la base, se lo oculta.
        public bool Activo { get; set; } = true;

        // Auditoría: quién generó este registro de pago y cuándo (no confundir con "Fecha",
        // que es la fecha del pago en sí, informada manualmente en pagos manuales).
        public string? RegistradoPorUserId { get; set; }
        public string? RegistradoPorNombre { get; set; }
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        // Auditoría: quién dejó el pago en su estado actual (aprobó/rechazó un comprobante,
        // o "Mercado Pago (automático)" si vino del webhook) y cuándo.
        public string? ActualizadoPorNombre { get; set; }
        public DateTime? FechaActualizacion { get; set; }
    }
    
}
