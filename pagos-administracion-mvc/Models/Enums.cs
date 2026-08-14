namespace pagos_administracion_mvc.Models
{
    public class Enums
    {
        public enum NivelEducativo
        {
            Primaria,
            Secundaria
        }
        public enum Turno
        {
            Mañana,
            Tarde
        }
        public enum EstadoCuota
        {
            Pendiente,
            Pagada,
            Vencida,
            Parcial
        }
        public enum EstadoPago
        {
            Pendiente,
            Aprobado,
            Rechazado,
            Cancelado
        }
    }
}
