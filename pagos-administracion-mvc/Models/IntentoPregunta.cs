using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace pagos_administracion_mvc.Models
{
    // La respuesta del alumno a una pregunta dentro de un intento de examen.
    public class IntentoPregunta
    {
        public int Id { get; set; }

        public int IntentoExamenId { get; set; }
        [ValidateNever]
        public IntentoExamen IntentoExamen { get; set; } = null!;

        public int PreguntaExamenId { get; set; }
        [ValidateNever]
        public PreguntaExamen PreguntaExamen { get; set; } = null!;

        // La opción elegida por el alumno. Null = no respondió.
        public int? OpcionRespuestaId { get; set; }
        [ValidateNever]
        public OpcionRespuesta? OpcionRespuesta { get; set; }

        // Se calcula al corregir: true si OpcionRespuesta.EsCorrecta == true.
        public bool EsCorrecta { get; set; } = false;
    }
}
