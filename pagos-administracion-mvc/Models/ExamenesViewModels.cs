using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace pagos_administracion_mvc.Models
{
    // ViewModel para crear o editar un Examen con sus Preguntas y Opciones de forma inline.
    public class ExamenCreateEditViewModel
    {
        public int Id { get; set; }

        [Required]
        public int CursoAsignaturaId { get; set; }

        [Required(ErrorMessage = "El título es obligatorio.")]
        [Display(Name = "Título")]
        [StringLength(120)]
        public string Titulo { get; set; } = string.Empty;

        [Display(Name = "Descripción / Instrucciones")]
        public string? Descripcion { get; set; }

        [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
        [Display(Name = "Disponible desde")]
        public DateTime FechaDesde { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "La fecha de cierre es obligatoria.")]
        [Display(Name = "Disponible hasta")]
        public DateTime FechaHasta { get; set; } = DateTime.Today.AddDays(7);

        [Display(Name = "Tiempo límite (minutos)")]
        [Range(1, 300)]
        public int? TiempoLimiteMinutos { get; set; }

        [Display(Name = "Nota mínima aprobatoria")]
        [Range(1, 10)]
        public decimal? NotaMinimaAprobatoria { get; set; }

        [Display(Name = "Solo un intento")]
        public bool SoloUnIntento { get; set; } = true;

        [Display(Name = "Orden aleatorio de preguntas")]
        public bool OrdenAleatorio { get; set; } = false;

        [Display(Name = "Mostrar resultado al terminar")]
        public bool MostrarResultado { get; set; } = true;

        [Display(Name = "Período")]
        public int? PeriodoId { get; set; }

        // Listas de selección para el formulario
        [ValidateNever]
        public SelectList? Periodos { get; set; }

        // Preguntas con opciones inline
        public List<PreguntaViewModel> Preguntas { get; set; } = new();

        // Datos de solo lectura para mostrar en el formulario
        public string? NombreCurso { get; set; }
        public string? NombreAsignatura { get; set; }
    }

    public class PreguntaViewModel
    {
        public int Id { get; set; }  // 0 = nueva

        [Required(ErrorMessage = "El enunciado es obligatorio.")]
        [Display(Name = "Enunciado")]
        [StringLength(600)]
        public string Enunciado { get; set; } = string.Empty;

        public int Orden { get; set; }

        [Range(1, 100)]
        public int Puntaje { get; set; } = 1;

        // Índice de la opción correcta (0 = A, 1 = B, …).
        [Required(ErrorMessage = "Debe marcar una opción correcta.")]
        public int OpcionCorrectaIndex { get; set; } = 0;

        public List<OpcionViewModel> Opciones { get; set; } = new()
        {
            new() { Letra = "A" },
            new() { Letra = "B" },
            new() { Letra = "C" },
            new() { Letra = "D" },
        };
    }

    public class OpcionViewModel
    {
        public int Id { get; set; }  // 0 = nueva

        [Required(ErrorMessage = "El texto de la opción es obligatorio.")]
        [StringLength(300)]
        public string Texto { get; set; } = string.Empty;

        public string Letra { get; set; } = string.Empty;
    }

    // ViewModel para que el alumno rinda el examen
    public class RendirExamenViewModel
    {
        public int ExamenId { get; set; }
        public int IntentoExamenId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public int? TiempoLimiteMinutos { get; set; }
        public DateTime FechaInicio { get; set; }

        public List<PreguntaRendirViewModel> Preguntas { get; set; } = new();
    }

    public class PreguntaRendirViewModel
    {
        public int PreguntaExamenId { get; set; }
        public string Enunciado { get; set; } = string.Empty;
        public int Orden { get; set; }
        public int Puntaje { get; set; }

        // La opción elegida por el alumno (se postea al enviar)
        public int? OpcionRespuestaIdElegida { get; set; }

        public List<OpcionRendirViewModel> Opciones { get; set; } = new();
    }

    public class OpcionRendirViewModel
    {
        public int Id { get; set; }
        public string Texto { get; set; } = string.Empty;
        public string Letra { get; set; } = string.Empty;
    }

    // ViewModel de resultado del intento (después de enviar)
    public class ResultadoExamenViewModel
    {
        public int IntentoExamenId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public decimal? Nota { get; set; }
        public bool? Aprobado { get; set; }
        public int PuntajeObtenido { get; set; }
        public int PuntajeTotal { get; set; }
        public bool MostrarDetalle { get; set; }
        public DateTime? FechaFin { get; set; }
        public TimeSpan? Duracion { get; set; }

        public List<PreguntaResultadoViewModel> Preguntas { get; set; } = new();
    }

    public class PreguntaResultadoViewModel
    {
        public string Enunciado { get; set; } = string.Empty;
        public int Puntaje { get; set; }
        public bool EsCorrecta { get; set; }
        public string? OpcionElegidaTexto { get; set; }
        public string? OpcionCorrectaTexto { get; set; }
        public string? OpcionElegidaLetra { get; set; }
        public string? OpcionCorrectaLetra { get; set; }
    }
}
