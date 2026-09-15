namespace pagos_administracion_mvc.Models
{
    /// <summary>
    /// Registro inmutable de cada cambio persistido en las entidades sensibles del sistema.
    /// Se genera automáticamente desde AdministracionDbContext.SaveChangesAsync — los
    /// controllers no necesitan saber que existe.
    /// </summary>
    public class AuditLog
    {
        public int Id { get; set; }

        /// <summary>
        /// Fecha y hora UTC del cambio (UTC para evitar ambigüedades con horario de verano
        /// en los servidores argentinos que pueden estar configurados con distintos TZ).
        /// </summary>
        public DateTime Fecha { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Id del ApplicationUser que originó el cambio. Null si el cambio lo hizo el
        /// RevisorVencimientosService u otro proceso sin contexto HTTP.
        /// </summary>
        public string? UsuarioId { get; set; }

        /// <summary>
        /// Email desnormalizado: se guarda en el momento del cambio para que el log mantenga
        /// sentido aunque el usuario sea dado de baja o cambie su email después.
        /// </summary>
        public string? UsuarioEmail { get; set; }

        /// <summary>
        /// Nombre del tipo C# afectado: "Nota", "Pago", "Cuota", "Alumno", "Inscripcion", "ArancelNivel".
        /// </summary>
        public string Entidad { get; set; } = string.Empty;

        /// <summary>
        /// Valor de la PK de la fila afectada, convertida a string para ser genérico
        /// (funciona con int, Guid, etc. sin cambiar el modelo de AuditLog).
        /// </summary>
        public string EntidadId { get; set; } = string.Empty;

        /// <summary>
        /// "Create", "Update" o "Delete".
        /// </summary>
        public string Accion { get; set; } = string.Empty;

        /// <summary>
        /// JSON con los valores de todas las propiedades escalares ANTES del cambio.
        /// Null en Create (no hay estado previo).
        /// </summary>
        public string? ValoresAnteriores { get; set; }

        /// <summary>
        /// JSON con los valores de todas las propiedades escalares DESPUÉS del cambio.
        /// Null en Delete (no hay estado posterior).
        /// </summary>
        public string? ValoresNuevos { get; set; }
    }
}
