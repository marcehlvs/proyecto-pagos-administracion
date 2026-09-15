namespace pagos_administracion_mvc.Models
{
    // Controla cuándo Familia/Alumno pueden ver el boletín. Se publica por Curso + AnioLectivo
    // (no por Alumno individual): el Preceptor o el Admin lo marcan como "listo" una vez que
    // cerraron las notas del curso, y recién ahí se habilita para todos los alumnos de ese curso.
    // Mientras no exista fila (o exista con Publicado = false), Familia/Alumno no pueden acceder
    // (ver BoletinesController.ValidarPermisoAsync). Admin y Preceptor siempre pueden, sin
    // importar este estado.
    public class BoletinPublicacion
    {
        public int Id { get; set; }

        public int CursoId { get; set; }
        public Curso Curso { get; set; } = null!;

        public int AnioLectivo { get; set; }

        public bool Publicado { get; set; }

        public DateTime? FechaPublicacion { get; set; }

        // Auditoría, mismo patrón que Nota/Asistencia/Tarea (guardamos el nombre, no solo el userId,
        // para que el dato sobreviva si el usuario se borra más adelante).
        public string? PublicadoPorNombre { get; set; }
    }
}
