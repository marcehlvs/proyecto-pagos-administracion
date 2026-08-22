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
            Pendiente,      // 0 - sin tocar
            Aprobado,       // 1 - vuelve a su valor original
            Rechazado,      // 2 - vuelve a su valor original
            Cancelado,      // 3 - vuelve a su valor original
            EnRevision      // 4 - nuevo, al final, no pisa nada existente
        }
    }
}
