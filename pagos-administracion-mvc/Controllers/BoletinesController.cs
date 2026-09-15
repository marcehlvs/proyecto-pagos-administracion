using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using pagos_administracion_mvc.Services;

namespace pagos_administracion_mvc.Controllers
{
    // Admin y Preceptor siempre tienen acceso (ver ValidarPermisoAsync). Familia solo el de sus
    // propios hijos (Alumno.FamiliaUserId) y Alumno solo el propio (Alumno.AlumnoUserId), pero
    // ADEMÁS solo cuando el Preceptor (o el Admin) publicó el boletín de ese Curso+AnioLectivo
    // (ver BoletinPublicacion / EstaPublicadoAsync). Hasta que eso pase, Familia/Alumno no ven
    // nada, ni siquiera que el boletín "existe".
    [Authorize(Roles = "Admin,Familia,Alumno,Preceptor")]
    public class BoletinesController : Controller
    {
        private readonly AdministracionDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly BoletinService _boletinService;
        private readonly IBoletinPlantilla _plantillaPredeterminada;
        private readonly BoletinPlantillaRite _plantillaRite;

        public BoletinesController(AdministracionDbContext context, UserManager<ApplicationUser> userManager,
            BoletinService boletinService, IBoletinPlantilla plantillaPredeterminada, BoletinPlantillaRite plantillaRite)
        {
            _context = context;
            _userManager = userManager;
            _boletinService = boletinService;
            _plantillaPredeterminada = plantillaPredeterminada;
            _plantillaRite = plantillaRite;
        }

        // true si TODOS los cursos en los que está inscripto el alumno tienen el boletín
        // publicado para ese año (lo normal es un solo curso; si tuviera más de uno, tienen que
        // estar todos listos para no mostrar un boletín "a medias").
        private async Task<bool> EstaPublicadoAsync(int alumnoId, int anioLectivo)
        {
            var cursosIds = await _context.Inscripciones
                .Where(i => i.AlumnoId == alumnoId)
                .Select(i => i.CursoId)
                .ToListAsync();

            if (cursosIds.Count == 0) return false;

            var publicados = await _context.BoletinPublicaciones
                .Where(bp => cursosIds.Contains(bp.CursoId) && bp.AnioLectivo == anioLectivo && bp.Publicado)
                .Select(bp => bp.CursoId)
                .ToListAsync();

            return cursosIds.All(id => publicados.Contains(id));
        }

        private async Task<IActionResult?> ValidarPermisoAsync(int alumnoId, int anioLectivo)
        {
            if (User.IsInRole("Admin")) return null;

            var userId = _userManager.GetUserId(User);
            var alumno = await _context.Alumnos.FindAsync(alumnoId);
            if (alumno == null) return NotFound();

            if (User.IsInRole("Preceptor"))
            {
                var esDeSuCurso = await _context.Inscripciones
                    .AnyAsync(i => i.AlumnoId == alumnoId && i.Curso.ProfesorUserId == userId);
                if (esDeSuCurso) return null;
                return Forbid();
            }

            var esPropio = (User.IsInRole("Familia") && alumno.FamiliaUserId == userId) ||
                            (User.IsInRole("Alumno") && alumno.AlumnoUserId == userId);
            if (!esPropio) return Forbid();

            // Es su hijo/el suyo propio, pero todavía no está publicado: no es un error de
            // permisos (Forbid), es "todavía no" -> NotFound para no filtrar que el boletín existe.
            if (!await EstaPublicadoAsync(alumnoId, anioLectivo)) return NotFound();

            return null;
        }

        // GET: Boletines?alumnoId=1&anioLectivo=2026
        // Pantalla simple: Admin elige cualquier alumno. Preceptor elige entre los alumnos de sus
        // cursos. Familia/Alumno arrancan con su(s) propio(s) alumno(s) ya resuelto(s).
        public async Task<IActionResult> Index(int? alumnoId, int? anioLectivo)
        {
            var anio = anioLectivo ?? DateTime.Today.Year;

            if (User.IsInRole("Admin"))
            {
                ViewBag.Alumnos = await _context.Alumnos.OrderBy(a => a.Apellido).ToListAsync();
            }
            else if (User.IsInRole("Preceptor"))
            {
                var userId = _userManager.GetUserId(User);
                ViewBag.Alumnos = await _context.Inscripciones
                    .Where(i => i.Curso.ProfesorUserId == userId)
                    .Select(i => i.Alumno)
                    .Distinct()
                    .OrderBy(a => a.Apellido)
                    .ToListAsync();
            }
            else if (User.IsInRole("Familia"))
            {
                var userId = _userManager.GetUserId(User);
                ViewBag.Alumnos = await _context.Alumnos.Where(a => a.FamiliaUserId == userId).OrderBy(a => a.Apellido).ToListAsync();
            }
            else // Alumno
            {
                var userId = _userManager.GetUserId(User);
                var propio = await _context.Alumnos.FirstOrDefaultAsync(a => a.AlumnoUserId == userId);
                ViewBag.Alumnos = propio != null ? new List<Alumno> { propio } : new List<Alumno>();
                alumnoId ??= propio?.Id;
            }

            // Para Familia/Alumno, la vista necesita saber si todavía no está publicado, para
            // mostrar "todavía no está disponible" en vez de un botón que va a dar 404.
            if (!User.IsInRole("Admin") && !User.IsInRole("Preceptor") && alumnoId.HasValue)
            {
                ViewBag.Publicado = await EstaPublicadoAsync(alumnoId.Value, anio);
            }
            else
            {
                ViewBag.Publicado = true;
            }

            ViewBag.AlumnoIdSeleccionado = alumnoId;
            ViewBag.AnioLectivo = anio;
            return View();
        }

        // GET: Boletines/Pdf?alumnoId=1&anioLectivo=2026&plantilla=rite
        // plantilla=rite usa el formulario oficial (RITE, Fase 1 — ver BoletinPlantillaRite);
        // cualquier otro valor (o ninguno) usa la plantilla propia de siempre.
        public async Task<IActionResult> Pdf(int alumnoId, int anioLectivo, string? plantilla)
        {
            var error = await ValidarPermisoAsync(alumnoId, anioLectivo);
            if (error != null) return error;

            var datos = await _boletinService.ObtenerDatosAsync(alumnoId, anioLectivo);
            if (datos == null) return NotFound();

            var configuracionSitio = await _context.ConfiguracionSitio.FirstOrDefaultAsync(c => c.Id == 1);
            byte[] pdf;
            if (plantilla == "rite")
            {
                pdf = _plantillaRite.Generar(datos, configuracionSitio);
            }
            else
            {
                pdf = _plantillaPredeterminada.Generar(datos, configuracionSitio);
            }

            var nombreArchivo = $"boletin-{datos.Alumno.Apellido}-{datos.Alumno.Nombre}-{anioLectivo}.pdf".Replace(" ", "_");
            return File(pdf, "application/pdf", nombreArchivo);
        }

        // GET: Boletines/Publicaciones?anioLectivo=2026
        // Pantalla para que Preceptor (sus cursos) o Admin (todos) marquen qué cursos ya tienen
        // el boletín listo para que lo vean Familia/Alumno.
        [Authorize(Roles = "Admin,Preceptor")]
        public async Task<IActionResult> Publicaciones(int? anioLectivo)
        {
            var anio = anioLectivo ?? DateTime.Today.Year;

            var cursos = User.IsInRole("Admin")
                ? await _context.Cursos.OrderBy(c => c.Nivel).ThenBy(c => c.GradoAnio).ToListAsync()
                : await _context.Cursos.Where(c => c.ProfesorUserId == _userManager.GetUserId(User))
                    .OrderBy(c => c.Nivel).ThenBy(c => c.GradoAnio).ToListAsync();

            var cursosIds = cursos.Select(c => c.Id).ToList();
            var publicaciones = await _context.BoletinPublicaciones
                .Where(bp => cursosIds.Contains(bp.CursoId) && bp.AnioLectivo == anio)
                .ToDictionaryAsync(bp => bp.CursoId);

            ViewBag.Cursos = cursos;
            ViewBag.Publicaciones = publicaciones;
            ViewBag.AnioLectivo = anio;
            return View();
        }

        // POST: Boletines/Publicar
        // Alterna Publicado para un Curso+AnioLectivo. Preceptor solo puede tocar sus propios
        // cursos (mismo criterio que el resto de las acciones); Admin, cualquiera. Al pasar a
        // publicado, dispara un Aviso para que Familia se entere sin tener que estar
        // entrando a mirar.
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Preceptor")]
        public async Task<IActionResult> Publicar(int cursoId, int anioLectivo, bool publicado)
        {
            var curso = await _context.Cursos.FindAsync(cursoId);
            if (curso == null) return NotFound();

            if (User.IsInRole("Preceptor") && curso.ProfesorUserId != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            var publicacion = await _context.BoletinPublicaciones
                .FirstOrDefaultAsync(bp => bp.CursoId == cursoId && bp.AnioLectivo == anioLectivo);

            var yaEstabaPublicado = publicacion?.Publicado ?? false;

            if (publicacion == null)
            {
                publicacion = new BoletinPublicacion { CursoId = cursoId, AnioLectivo = anioLectivo };
                _context.BoletinPublicaciones.Add(publicacion);
            }

            publicacion.Publicado = publicado;
            publicacion.FechaPublicacion = publicado ? DateTime.Now : null;
            publicacion.PublicadoPorNombre = publicado ? User.Identity?.Name : null;

            // Aviso solo la primera vez que pasa a publicado (no en cada toggle on/off).
            if (publicado && !yaEstabaPublicado)
            {
                _context.Avisos.Add(new Aviso
                {
                    Titulo = $"Boletín disponible — {curso.Etiqueta}",
                    Descripcion = $"Ya está disponible el boletín de {curso.Etiqueta} correspondiente al ciclo lectivo {anioLectivo}. Podés verlo desde la sección Boletín.",
                    Tipo = TipoAviso.Importante
                });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Publicaciones), new { anioLectivo });
        }
    }
}
