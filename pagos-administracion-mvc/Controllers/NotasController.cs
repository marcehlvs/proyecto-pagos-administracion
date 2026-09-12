using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using pagos_administracion_mvc.Services;
using System.Globalization;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Controllers
{
    // Ver notas: Admin (cualquier materia) o el Docente asignado a esa materia.
    // Cargar/editar notas: SOLO el Docente asignado a esa materia — el Admin puede ver, pero
    // no modificar (pedido explícito), aunque también tenga el rol Docente en otra materia.
    [Authorize(Roles = "Admin,Docente")]
    public class NotasController : Controller
    {
        private readonly AdministracionDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly NotaCalculadora _calculadora;

        public NotasController(AdministracionDbContext context, UserManager<ApplicationUser> userManager, NotaCalculadora calculadora)
        {
            _context = context;
            _userManager = userManager;
            _calculadora = calculadora;
        }

        private async Task<(CursoAsignatura? cursoAsignatura, bool esDocentePropio, IActionResult? error)> ObtenerConPermisoAsync(int cursoAsignaturaId, bool soloLectura)
        {
            var cursoAsignatura = await _context.CursosAsignaturas
                .Include(ca => ca.Curso)
                .Include(ca => ca.Asignatura)
                .FirstOrDefaultAsync(ca => ca.Id == cursoAsignaturaId);
            if (cursoAsignatura == null) return (null, false, NotFound());

            var esDocentePropio = User.IsInRole("Docente") && cursoAsignatura.DocenteUserId == _userManager.GetUserId(User);
            var esAdmin = User.IsInRole("Admin");

            if (soloLectura)
            {
                if (!esAdmin && !esDocentePropio) return (null, false, Forbid());
            }
            else if (!esDocentePropio)
            {
                // Cargar/Guardar/Recalcular: ni el Admin puede, salvo que también sea el Docente de esta materia.
                return (null, false, Forbid());
            }

            return (cursoAsignatura, esDocentePropio, null);
        }

        // GET: Notas — mis materias (Docente) o todas (Admin), para elegir dónde ver/cargar notas.
        public async Task<IActionResult> Index()
        {
            var query = _context.CursosAsignaturas
                .Include(ca => ca.Curso)
                .Include(ca => ca.Asignatura)
                .AsQueryable();

            if (User.IsInRole("Docente") && !User.IsInRole("Admin"))
            {
                var userId = _userManager.GetUserId(User);
                query = query.Where(ca => ca.DocenteUserId == userId);
            }

            var materias = await query
                .OrderBy(ca => ca.Curso.Nivel).ThenBy(ca => ca.Curso.GradoAnio).ThenBy(ca => ca.Asignatura.Nombre)
                .ToListAsync();

            return View(materias);
        }

        // GET: Notas/Cargar?cursoAsignaturaId=1&periodoId=3&columnas=4
        // Sin periodoId: solo pide elegir el Periodo. Con periodoId:
        //  - Si el Periodo tiene Subperiodos (ej. un Cuatrimestre hecho de Trimestres): una sola
        //    nota consolidada por alumno (calculada, o forzada a mano).
        //  - Si NO tiene Subperiodos (ej. un Trimestre): una grilla de "columnas" notas sueltas
        //    por alumno (mínimo 4, ampliable), más el promedio resultante.
        public async Task<IActionResult> Cargar(int cursoAsignaturaId, int? periodoId, int columnas = 4)
        {
            var (cursoAsignatura, esDocentePropio, error) = await ObtenerConPermisoAsync(cursoAsignaturaId, soloLectura: true);
            if (error != null) return error;

            var anioActual = DateTime.Today.Year;
            var modelo = new CargarNotasViewModel
            {
                CursoAsignatura = cursoAsignatura!,
                SoloLectura = !esDocentePropio,
                PeriodosDisponibles = await _context.Periodos
                    .Where(p => p.AnioLectivo == anioActual)
                    .OrderBy(p => p.Tipo).ThenBy(p => p.Nombre)
                    .ToListAsync()
            };

            if (periodoId.HasValue)
            {
                modelo.PeriodoSeleccionado = await _context.Periodos
                    .Include(p => p.Subperiodos)
                    .FirstOrDefaultAsync(p => p.Id == periodoId);
                if (modelo.PeriodoSeleccionado == null) return NotFound();

                var inscripciones = await _context.Inscripciones
                    .Include(i => i.Alumno)
                    .Where(i => i.CursoId == cursoAsignatura!.CursoId)
                    .OrderBy(i => i.Alumno.Apellido)
                    .ToListAsync();

                if (!modelo.EsPeriodoContenedor)
                {
                    var maxOrdenExistente = await _context.Notas
                        .Where(n => n.CursoAsignaturaId == cursoAsignaturaId && n.PeriodoId == periodoId && n.Orden > 0)
                        .Select(n => (int?)n.Orden)
                        .MaxAsync() ?? 0;
                    modelo.Columnas = Math.Max(columnas, Math.Max(maxOrdenExistente, 4));
                }

                foreach (var inscripcion in inscripciones)
                {
                    var fila = new FilaAlumnoNota
                    {
                        InscripcionId = inscripcion.Id,
                        AlumnoNombre = $"{inscripcion.Alumno.Apellido}, {inscripcion.Alumno.Nombre}"
                    };

                    var notasDeEstePeriodo = await _context.Notas
                        .Where(n => n.InscripcionId == inscripcion.Id && n.CursoAsignaturaId == cursoAsignaturaId && n.PeriodoId == periodoId)
                        .ToListAsync();

                    if (!modelo.EsPeriodoContenedor)
                    {
                        foreach (var n in notasDeEstePeriodo.Where(n => n.Orden > 0))
                            fila.ValoresPorOrden[n.Orden] = n.Valor;
                    }

                    fila.Promedio = await _calculadora.CalcularAsync(inscripcion.Id, cursoAsignaturaId, periodoId.Value);
                    var consolidada = notasDeEstePeriodo.FirstOrDefault(n => n.Orden == 0);
                    fila.EsPromedioAutomatico = consolidada?.EsPromedioAutomatico ?? true;
                    fila.ValoracionPreliminar = consolidada?.ValoracionPreliminar;

                    modelo.Filas.Add(fila);
                }
            }

            return View(modelo);
        }

        // POST: Notas/Guardar
        // notasSueltas: una entrada por (alumno, columna) con Orden 1..N. Solo tiene sentido si
        // el Periodo no es contenedor (una nota suelta por columna, tantas como el Docente haya
        // cargado). notaManual: fuerza la nota consolidada del Periodo (Orden 0) a mano, sea
        // contenedor o no — por ejemplo, para "no se coloca la real sino que se tiene en cuenta
        // todo el cuatrimestre". Si se deja vacío, la consolidada sigue saliendo del promedio
        // automático.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar(int cursoAsignaturaId, int periodoId,
            List<NotaSueltaInput>? notasSueltas, List<NotaManualInput>? notaManual)
        {
            var (cursoAsignatura, _, error) = await ObtenerConPermisoAsync(cursoAsignaturaId, soloLectura: false);
            if (error != null) return error;

            var nombreDocente = User.Identity?.Name ?? "Docente";
            var inscripcionesTocadas = new HashSet<int>();

            foreach (var entrada in notasSueltas ?? new List<NotaSueltaInput>())
            {
                var valor = ParsearValor(entrada.Valor);
                if (entrada.Orden <= 0 || !valor.HasValue || valor < 0 || valor > 10) continue;
                await GuardarUnaAsync(entrada.InscripcionId, cursoAsignaturaId, periodoId, entrada.Orden, valor.Value, nombreDocente, manual: true);
                inscripcionesTocadas.Add(entrada.InscripcionId);
            }

            foreach (var entrada in notaManual ?? new List<NotaManualInput>())
            {
                var valor = ParsearValor(entrada.Valor);
                if (valor.HasValue && (valor < 0 || valor > 10)) continue;
                var valoracion = ParsearValoracion(entrada.ValoracionPreliminar);

                var seGuardoAlgo = await GuardarConsolidadaAsync(entrada.InscripcionId, cursoAsignaturaId, periodoId, valor, valoracion, nombreDocente);
                if (seGuardoAlgo) inscripcionesTocadas.Add(entrada.InscripcionId);
            }

            // Sube por la cadena de Periodo (este -> su padre -> el padre del padre...)
            // recalculando la nota consolidada en cada nivel, para que el boletín no muestre
            // un Trimestre/Cuatrimestre desactualizado.
            foreach (var inscripcionId in inscripcionesTocadas)
                await _calculadora.RecalcularHaciaArribaAsync(inscripcionId, cursoAsignaturaId, periodoId);

            TempData["Mensaje"] = inscripcionesTocadas.Count > 0
                ? $"Se guardaron notas de {inscripcionesTocadas.Count} alumno(s)."
                : "No había ninguna nota nueva para guardar.";
            return RedirectToAction(nameof(Cargar), new { cursoAsignaturaId, periodoId });
        }

        // Los <input type="number"> del navegador siempre mandan el valor con "." como separador
        // decimal, sin importar el idioma de la página — pero si esta propiedad llegara como
        // decimal? directamente, ASP.NET Core la parsearía con la cultura del servidor (es-AR:
        // coma decimal, punto de miles), y "8.5" podría bindear como null o como 85. Por eso
        // llega como string y se parsea acá, a mano, siempre en cultura invariante. De paso
        // acepta "8,5" por si alguien lo escribe con coma sin querer.
        private static decimal? ParsearValor(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            texto = texto.Trim().Replace(',', '.');
            return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor) ? valor : null;
        }

        private static ValoracionPreliminar? ParsearValoracion(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            return Enum.TryParse<ValoracionPreliminar>(texto, out var valoracion) ? valoracion : null;
        }

        private async Task GuardarUnaAsync(int inscripcionId, int cursoAsignaturaId, int periodoId, int orden, decimal valor, string nombreDocente, bool manual)
        {
            var nota = await _context.Notas.FirstOrDefaultAsync(n =>
                n.InscripcionId == inscripcionId && n.CursoAsignaturaId == cursoAsignaturaId &&
                n.PeriodoId == periodoId && n.Orden == orden);

            if (nota != null)
            {
                nota.Valor = valor;
                if (manual) nota.EsPromedioAutomatico = false;
                nota.ModificadaPorNombre = nombreDocente;
                nota.FechaModificacion = DateTime.Now;
            }
            else
            {
                _context.Notas.Add(new Nota
                {
                    InscripcionId = inscripcionId,
                    CursoAsignaturaId = cursoAsignaturaId,
                    PeriodoId = periodoId,
                    Orden = orden,
                    Valor = valor,
                    EsPromedioAutomatico = !manual,
                    CargadaPorNombre = nombreDocente
                });
            }
            await _context.SaveChangesAsync();
        }

        // Guarda la fila Orden = 0 (consolidada) de un alumno: el override numérico ("Forzar
        // nota final") y/o la Valoración Preliminar son independientes entre sí, así que un
        // Docente puede cargar solo una de las dos sin tocar la otra. valorManual == null
        // significa "no tocar el número" (sigue saliendo del promedio automático si ya lo
        // estaba); valoracion == null significa "sin Valoración Preliminar" y SÍ la borra si
        // antes había una cargada (el Docente la vació a propósito). Devuelve false si no había
        // nada para guardar (fila sin tocar, no hace falta recalcular nada para ese alumno).
        private async Task<bool> GuardarConsolidadaAsync(int inscripcionId, int cursoAsignaturaId, int periodoId,
            decimal? valorManual, ValoracionPreliminar? valoracion, string nombreDocente)
        {
            var nota = await _context.Notas.FirstOrDefaultAsync(n =>
                n.InscripcionId == inscripcionId && n.CursoAsignaturaId == cursoAsignaturaId &&
                n.PeriodoId == periodoId && n.Orden == 0);

            if (nota == null)
            {
                if (!valorManual.HasValue && valoracion == null) return false; // nada para crear.
                _context.Notas.Add(new Nota
                {
                    InscripcionId = inscripcionId,
                    CursoAsignaturaId = cursoAsignaturaId,
                    PeriodoId = periodoId,
                    Orden = 0,
                    // Placeholder si solo se cargó Valoración Preliminar sin forzar un número:
                    // NotaCalculadora la recalcula sola (EsPromedioAutomatico = true) apenas haya
                    // Subperiodos o notas sueltas de dónde promediar, y hasta entonces el 0 nunca
                    // se muestra (CalcularAsync devuelve null si no hay nada para promediar).
                    Valor = valorManual ?? 0,
                    EsPromedioAutomatico = !valorManual.HasValue,
                    ValoracionPreliminar = valoracion,
                    CargadaPorNombre = nombreDocente
                });
            }
            else
            {
                if (valorManual.HasValue)
                {
                    nota.Valor = valorManual.Value;
                    nota.EsPromedioAutomatico = false;
                }
                nota.ValoracionPreliminar = valoracion; // se pisa siempre: vacío = "la borré".
                nota.ModificadaPorNombre = nombreDocente;
                nota.FechaModificacion = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        // POST: Notas/Recalcular — descarta la nota consolidada manual y vuelve a dejar que el
        // Periodo se calcule solo (promedio de sus notas sueltas o de sus Subperiodos).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Recalcular(int cursoAsignaturaId, int periodoId, int inscripcionId)
        {
            var (cursoAsignatura, _, error) = await ObtenerConPermisoAsync(cursoAsignaturaId, soloLectura: false);
            if (error != null) return error;

            var nota = await _context.Notas.FirstOrDefaultAsync(n =>
                n.InscripcionId == inscripcionId && n.CursoAsignaturaId == cursoAsignaturaId &&
                n.PeriodoId == periodoId && n.Orden == 0);
            if (nota != null)
            {
                nota.EsPromedioAutomatico = true;
                await _context.SaveChangesAsync();
            }

            await _calculadora.CalcularAsync(inscripcionId, cursoAsignaturaId, periodoId);
            return RedirectToAction(nameof(Cargar), new { cursoAsignaturaId, periodoId });
        }
    }
}
