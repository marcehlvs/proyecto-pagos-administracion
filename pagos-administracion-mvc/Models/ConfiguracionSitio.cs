using System.ComponentModel.DataAnnotations;

namespace pagos_administracion_mvc.Models
{
    // Fila única (Id = 1) con la paleta de colores "de marca" del sitio. A diferencia del switch
    // claro/oscuro (que es una preferencia del navegador, guardada en localStorage), esto es
    // institucional: lo define el Admin y se ve igual para todos los usuarios.
    // Solo cubre los 4 colores que NO cambian entre modo claro/oscuro (ColorPrimario,
    // ColorPrimarioOscuro, ColorExito, ColorAdvertencia); fondo/superficie/texto siguen
    // controlados por el switch de tema para no romper el contraste en modo oscuro.
    public class ConfiguracionSitio
    {
        public int Id { get; set; }

        [Display(Name = "Color primario")]
        [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Tiene que ser un color hexadecimal, ej: #1A365D")]
        public string ColorPrimario { get; set; } = "#1A365D";

        [Display(Name = "Color primario (oscuro / navbar)")]
        [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Tiene que ser un color hexadecimal, ej: #002045")]
        public string ColorPrimarioOscuro { get; set; } = "#002045";

        [Display(Name = "Color de éxito (pagos al día)")]
        [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Tiene que ser un color hexadecimal, ej: #10B981")]
        public string ColorExito { get; set; } = "#10B981";

        [Display(Name = "Color de advertencia (vencimientos)")]
        [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Tiene que ser un color hexadecimal, ej: #F59E0B")]
        public string ColorAdvertencia { get; set; } = "#F59E0B";

        [Display(Name = "Nombre del estilo")]
        [MaxLength(50)]
        public string NombrePreset { get; set; } = "Institucional (por defecto)";

        // Distrito DGCyE del establecimiento, para el encabezado del boletín RITE. Uno solo para
        // todo el colegio (a diferencia de Sección, que sale del Curso de cada alumno).
        [Display(Name = "Distrito")]
        [MaxLength(100)]
        public string? Distrito { get; set; }

        public string? ModificadaPorNombre { get; set; }
        public DateTime? FechaModificacion { get; set; }
    }
}
