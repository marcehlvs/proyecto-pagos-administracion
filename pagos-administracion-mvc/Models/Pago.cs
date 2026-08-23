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
    }
    
}
