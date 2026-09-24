using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Controllers
{
    // Rol Alumno y Familia: ver y rendir exámenes disponibles.
    // Mismo patrón de permisos que MisTareasController.
    [Authorize(Roles = "Alumno,Familia")]
    public class MisExamenesController : Controller
    {
        private readonly AdministracionDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MisExamenesController(AdministracionDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<List<Alumno>> MisAlumnosAsync()
        {
            var userId = _userManager.GetUserId(User);
            var query = User.IsInRole("Alumno")
                ? _context.Alumnos.Where(a => a.AlumnoUserId == userId)
                : _context.Alumnos.Where(a => a.FamiliaUserId == userId);
            return await query.OrderBy(a => a.Apellido).ToListAsync();
        }

        // GET: MisExamenes?alumnoId=5
        public async Task<IActionResult> Index(int? alumnoId)
        {
            var misAlumnos = await MisAlumnosAsync();
            if (misAlumnos.Count == 0) return NotFound();

            var alumno = alumnoId.HasValue
                ? misAlumnos.FirstOrDefault(a => a.Id == alumnoId.Value)
                : misAlumnos.First();
            if (alumno == null) return Forbid();

            ViewBag.MisAlumnos = misAlumnos;
            ViewBag.AlumnoSeleccionado = alumno;

            // Inscripciones activas del alumno
            var inscripciones = await _context.Inscripciones
                .Where(i => i.AlumnoId == alumno.Id)
                .Include(i => i.Curso)
                .ToListAsync();

            var inscripcionIds = inscripciones.Select(i => i.Id).ToList();
            var cursoAsignaturaIds = await _context.CursosAsignaturas
                .Where(ca => inscripciones.Select(i => i.CursoId).Contains(ca.CursoId))
                .Select(ca => ca.Id)
                .ToListAsync();

            var ahora = DateTime.Now;

            // Exámenes disponibles para el alumno
            var examenes = await _context.Examenes
                .Where(e => cursoAsignaturaIds.Contains(e.CursoAsignaturaId))
                .Include(e => e.CursoAsignatura).ThenInclude(ca => ca.Asignatura)
                .Include(e => e.CursoAsignatura).ThenInclude(ca => ca.Curso)
                .Include(e => e.Preguntas.Where(p => p.Activo))
                .Include(e => e.Intentos.Where(i => inscripcionIds.Contains(i.InscripcionId)))
                .OrderByDescending(e => e.FechaDesde)
                .ToListAsync();

            ViewBag.InscripcionIds = inscripcionIds;
            ViewBag.Ahora = ahora;

            return View(examenes);
        }

        // GET: MisExamenes/Rendir/5?alumnoId=3
        // Inicia o reanuda un intento de examen.
        [HttpGet]
        public async Task<IActionResult> Rendir(int id, int alumnoId)
        {
            var misAlumnos = await MisAlumnosAsync();
            var alumno = misAlumnos.FirstOrDefault(a => a.Id == alumnoId);
            if (alumno == null) return Forbid();

            var inscripcion = await _context.Inscripciones
                .Include(i => i.Curso)
                .FirstOrDefaultAsync(i => i.AlumnoId == alumnoId);
            if (inscripcion == null) return NotFound();

            var examen = await _context.Examenes
                .Include(e => e.Preguntas.Where(p => p.Activo))
                    .ThenInclude(p => p.Opciones)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (examen == null) return NotFound();

            var ahora = DateTime.Now;
            if (ahora < examen.FechaDesde || ahora > examen.FechaHasta)
            {
                TempData["Error"] = "Este examen no está disponible en este momento.";
                return RedirectToAction(nameof(Index), new { alumnoId });
            }

            // Verificar si ya tiene un intento completo y si solo se permite uno
            var intentoExistente = await _context.IntentosExamen
                .FirstOrDefaultAsync(i => i.ExamenId == id && i.InscripcionId == inscripcion.Id);

            if (intentoExistente != null && intentoExistente.FechaFin != null && examen.SoloUnIntento)
            {
                TempData["Error"] = "Ya completaste este examen y no se permiten más intentos.";
                return RedirectToAction(nameof(Resultado), new { id = intentoExistente.Id, alumnoId });
            }

            // Si hay un intento en curso (sin FechaFin), retomarlo
            IntentoExamen intento;
            if (intentoExistente != null && intentoExistente.FechaFin == null)
            {
                intento = intentoExistente;
            }
            else
            {
                // Crear nuevo intento
                intento = new IntentoExamen
                {
                    ExamenId = examen.Id,
                    InscripcionId = inscripcion.Id,
                    FechaInicio = ahora,
                    PuntajeTotal = examen.Preguntas.Sum(p => p.Puntaje),
                };
                _context.IntentosExamen.Add(intento);
                await _context.SaveChangesAsync();

                // Pre-crear las filas de IntentoPregunta (una por pregunta, sin respuesta aún)
                foreach (var pregunta in examen.Preguntas.Where(p => p.Activo))
                {
                    _context.IntentosPreguntas.Add(new IntentoPregunta
                    {
                        IntentoExamenId = intento.Id,
                        PreguntaExamenId = pregunta.Id,
                    });
                }
                await _context.SaveChangesAsync();
            }

            // Construir ViewModel
            var preguntas = examen.Preguntas
                .Where(p => p.Activo)
                .OrderBy(p => examen.OrdenAleatorio ? Guid.NewGuid() : Guid.Empty)
                .ThenBy(p => p.Orden)
                .ToList();

            var vm = new RendirExamenViewModel
            {
                ExamenId = examen.Id,
                IntentoExamenId = intento.Id,
                Titulo = examen.Titulo,
                Descripcion = examen.Descripcion,
                TiempoLimiteMinutos = examen.TiempoLimiteMinutos,
                FechaInicio = intento.FechaInicio,
                Preguntas = preguntas.Select((p, idx) => new PreguntaRendirViewModel
                {
                    PreguntaExamenId = p.Id,
                    Enunciado = p.Enunciado,
                    Orden = idx + 1,
                    Puntaje = p.Puntaje,
                    Opciones = p.Opciones.OrderBy(o => o.Letra).Select(o => new OpcionRendirViewModel
                    {
                        Id = o.Id,
                        Texto = o.Texto,
                        Letra = o.Letra,
                    }).ToList()
                }).ToList()
            };

            ViewBag.AlumnoId = alumnoId;
            return View(vm);
        }

        // POST: MisExamenes/Enviar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enviar(int intentoExamenId, int alumnoId,
            Dictionary<int, int> respuestas) // key = PreguntaExamenId, value = OpcionRespuestaId
        {
            var misAlumnos = await MisAlumnosAsync();
            var alumno = misAlumnos.FirstOrDefault(a => a.Id == alumnoId);
            if (alumno == null) return Forbid();

            var intento = await _context.IntentosExamen
                .Include(i => i.Examen)
                    .ThenInclude(e => e.Preguntas.Where(p => p.Activo))
                    .ThenInclude(p => p.Opciones)
                .Include(i => i.Respuestas)
                .FirstOrDefaultAsync(i => i.Id == intentoExamenId);

            if (intento == null || intento.FechaFin != null) return NotFound();

            // Verificar tiempo límite
            if (intento.Examen.TiempoLimiteMinutos.HasValue)
            {
                var tiempoTranscurrido = DateTime.Now - intento.FechaInicio;
                if (tiempoTranscurrido.TotalMinutes > intento.Examen.TiempoLimiteMinutos.Value + 1)
                {
                    // Tolerar 1 min extra para latencia de red
                    TempData["Error"] = "El tiempo del examen ha vencido. Se guardaron las respuestas enviadas hasta el momento.";
                }
            }

            // Registrar respuestas y corregir
            int puntajeObtenido = 0;

            foreach (var ip in intento.Respuestas)
            {
                if (respuestas.TryGetValue(ip.PreguntaExamenId, out int opcionId))
                {
                    var opcion = intento.Examen.Preguntas
                        .SelectMany(p => p.Opciones)
                        .FirstOrDefault(o => o.Id == opcionId);

                    ip.OpcionRespuestaId = opcionId;
                    ip.EsCorrecta = opcion?.EsCorrecta ?? false;

                    if (ip.EsCorrecta)
                    {
                        var pregunta = intento.Examen.Preguntas
                            .First(p => p.Id == ip.PreguntaExamenId);
                        puntajeObtenido += pregunta.Puntaje;
                    }
                }
            }

            // Calcular nota (escala 0–10)
            int puntajeTotal = intento.Examen.Preguntas.Sum(p => p.Puntaje);
            decimal nota = puntajeTotal > 0
                ? Math.Round(puntajeObtenido * 10m / puntajeTotal, 2)
                : 0;

            bool aprobado = intento.Examen.NotaMinimaAprobatoria.HasValue
                ? nota >= intento.Examen.NotaMinimaAprobatoria.Value
                : nota >= 6m;

            intento.PuntajeObtenido = puntajeObtenido;
            intento.PuntajeTotal = puntajeTotal;
            intento.NotaValor = nota;
            intento.Aprobado = aprobado;
            intento.FechaFin = DateTime.Now;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Resultado), new { id = intentoExamenId, alumnoId });
        }

        // GET: MisExamenes/Resultado/5?alumnoId=3
        public async Task<IActionResult> Resultado(int id, int alumnoId)
        {
            var misAlumnos = await MisAlumnosAsync();
            var alumno = misAlumnos.FirstOrDefault(a => a.Id == alumnoId);
            if (alumno == null) return Forbid();

            var intento = await _context.IntentosExamen
                .Include(i => i.Examen)
                .Include(i => i.Respuestas)
                    .ThenInclude(r => r.PreguntaExamen)
                    .ThenInclude(p => p.Opciones)
                .Include(i => i.Respuestas)
                    .ThenInclude(r => r.OpcionRespuesta)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (intento == null) return NotFound();
            if (intento.FechaFin == null)
                return RedirectToAction(nameof(Rendir), new { id = intento.ExamenId, alumnoId });

            var vm = new ResultadoExamenViewModel
            {
                IntentoExamenId = intento.Id,
                Titulo = intento.Examen.Titulo,
                Nota = intento.NotaValor,
                Aprobado = intento.Aprobado,
                PuntajeObtenido = intento.PuntajeObtenido,
                PuntajeTotal = intento.PuntajeTotal,
                MostrarDetalle = intento.Examen.MostrarResultado,
                FechaFin = intento.FechaFin,
                Duracion = intento.FechaFin - intento.FechaInicio,
                Preguntas = intento.Examen.MostrarResultado
                    ? intento.Respuestas
                        .OrderBy(r => r.PreguntaExamen.Orden)
                        .Select(r =>
                        {
                            var correcta = r.PreguntaExamen.Opciones.FirstOrDefault(o => o.EsCorrecta);
                            return new PreguntaResultadoViewModel
                            {
                                Enunciado = r.PreguntaExamen.Enunciado,
                                Puntaje = r.PreguntaExamen.Puntaje,
                                EsCorrecta = r.EsCorrecta,
                                OpcionElegidaTexto = r.OpcionRespuesta?.Texto,
                                OpcionElegidaLetra = r.OpcionRespuesta?.Letra,
                                OpcionCorrectaTexto = correcta?.Texto,
                                OpcionCorrectaLetra = correcta?.Letra,
                            };
                        }).ToList()
                    : new List<PreguntaResultadoViewModel>()
            };

            ViewBag.AlumnoId = alumnoId;
            ViewBag.Alumno = alumno;
            return View(vm);
        }
    }
}
