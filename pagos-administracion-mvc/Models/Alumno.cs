using pagos_administracion_mvc.Data;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Models
{
    public class Alumno
    {
        public int Id { get; set; }
        public string Nombre { get; set; } =string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Dni { get; set; }=string.Empty;
        public NivelEducativo Nivel { get; set; }
        public int GradoAnio { get; set; }
        public Turno Turno { get; set; }

        public string? FamiliaUserId { get; set; }
        public ApplicationUser? FamiliaUser { get; set; }

        //Un alumno tiene muchas cuotas (una por mes)
        public ICollection<Cuota> Cuotas { get; set; } = new List<Cuota>();
    }
}
