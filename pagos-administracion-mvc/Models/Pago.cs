using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Models
{
    public class Pago
    {
        public int Id { get; set; }
        public int CuotaId { get; set; } 
        public Cuota Cuota { get; set; }
        public decimal Monto { get; set; }
        public DateTime Fecha {  get; set; }
        public EstadoPago Estado { get; set; }

        public string MercadoPagoPaymentId { get; set; }
        public string MercadoPagoPreferenceId { get; set; }
    }
    
}
