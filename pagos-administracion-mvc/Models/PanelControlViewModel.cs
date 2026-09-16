using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Models
{
    /// <summary>
    /// ViewModel compuesto para el Panel de Control Unificado del administrador.
    /// Agrupa las tres secciones del sidebar en un solo objeto que el controller
    /// pasa a la vista en un único viaje a la base de datos.
    /// </summary>
    public class PanelControlViewModel
    {
        // ── Sección 1: Métricas de pagos ──────────────────────────────────────
        public DashboardViewModel Metricas { get; set; } = new();

        // ── Sección 2: Auditoría ──────────────────────────────────────────────
        public List<AuditLog> UltimosAuditLogs { get; set; } = new();
        public int TotalAuditLogs { get; set; }
        public int PaginaAudit { get; set; } = 1;
        public int TamanioPaginaAudit { get; set; } = 25;

        // Filtros aplicados (para conservar el estado en el form de la vista).
        public string? FiltroEntidad { get; set; }
        public string? FiltroAccion { get; set; }

        public int TotalPaginasAudit =>
            TamanioPaginaAudit > 0
                ? (int)Math.Ceiling((double)TotalAuditLogs / TamanioPaginaAudit)
                : 1;

        // ── Sección 3: Configuración del sistema ──────────────────────────────
        public ConfiguracionSitio? ConfigSitio { get; set; }

        // Sección activa al cargar la página (se controla por query string ?tab=).
        // Valores válidos: "metricas" | "auditoria" | "configuracion"
        public string TabActiva { get; set; } = "metricas";
    }
}
