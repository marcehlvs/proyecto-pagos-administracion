using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Models
{
    public class Cuota
    {
        public int Id { get; set; }
        public int AlumnoId { get; set; }
        public Alumno Alumno { get; set; }
        public int Mes { get; set; }
        public int Anio { get; set; }
        public decimal Monto { get; set; } 
        public DateTime FechaVencimiento { get; set; }
        public EstadoCuota Estado { get; set; } = EstadoCuota.Pendiente;
        //Una cuota puede tener varios pagos parciales o reintentos
        public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    }
}
