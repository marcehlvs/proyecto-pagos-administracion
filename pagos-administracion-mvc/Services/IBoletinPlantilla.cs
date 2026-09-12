using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Services
{
    // Contrato de una "plantilla" de boletín: recibe los datos ya calculados (BoletinService) y
    // la paleta institucional del sitio (ConfiguracionSitio, puede no existir todavía) y devuelve
    // el PDF armado. Separar esto de BoletinesController permite tener más de una plantilla en el
    // futuro (por ej. una más compacta, o una con el diseño anterior) y elegir cuál es la
    // predeterminada en un solo lugar: el registro en Program.cs
    // (builder.Services.AddScoped<IBoletinPlantilla, BoletinPlantillaPredeterminada>()).
    public interface IBoletinPlantilla
    {
        byte[] Generar(BoletinData datos, ConfiguracionSitio? configuracionSitio);
    }
}
