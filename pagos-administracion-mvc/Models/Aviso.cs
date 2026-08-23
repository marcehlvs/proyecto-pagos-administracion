using System.ComponentModel.DataAnnotations;

namespace pagos_administracion_mvc.Models
{
    public enum TipoAviso { Importante, Calendario, Aviso }

    public class Aviso
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Titulo { get; set; } = string.Empty;
        
        [Required, StringLength(500)]
        public string Descripcion { get; set; } = string.Empty;

        [Display(Name = "Fecha de Publicación")]
        public DateTime FechaPublicacion { get; set; } = DateTime.Now;

        public bool Activo { get; set; } = true;
    }
}