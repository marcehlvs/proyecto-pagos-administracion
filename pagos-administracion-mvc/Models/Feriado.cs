using System.ComponentModel.DataAnnotations;

namespace pagos_administracion_mvc.Models
{
    // Feriados, días no laborables y feriados "puente" del calendario oficial (nacional, y
    // provincial/local si hiciera falta). BoletinService los cruza con el rango FechaInicio/
    // FechaFin de cada Periodo Cuatrimestral para descontarlos de "Días hábiles" del RITE, además
    // de los sábados y domingos. Se cargan una vez por año (los puentes turísticos recién se
    // decretan a fines del año anterior, así que no hay forma de calcularlos solos).
    public class Feriado
    {
        public int Id { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha")]
        public DateTime Fecha { get; set; }

        [Required, StringLength(120)]
        [Display(Name = "Descripción")]
        public string Descripcion { get; set; } = string.Empty;

        // Soft delete: mismo criterio que el resto del proyecto.
        public bool Activo { get; set; } = true;
    }
}
