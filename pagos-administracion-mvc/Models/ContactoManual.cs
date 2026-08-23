namespace pagos_administracion_mvc.Models
{
    public class ContactoManual
    {
        public int Id { get; set; }
        public int CuotaId { get; set; }
        public Cuota Cuota { get; set; } = null!;
        public DateTime Fecha { get; set; } = DateTime.Now;
        public string Medio { get; set; } = "WhatsApp"; // WhatsApp, Llamada, Presencial
        public string? Notas { get; set; }
    }
}