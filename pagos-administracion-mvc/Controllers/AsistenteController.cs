using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using pagos_administracion_mvc.Services;
using System.Text.Json;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Controllers
{
    // Único endpoint para Familia y Alumno. El rol logueado decide qué set de tools
    // se le pasa al modelo, así la IA nunca "sabe" que existen las tools del otro rol.
    // Cada tool revalida el dueño real de los datos contra FamiliaUserId/AlumnoUserId
    // (nunca contra lo que el modelo pida) — mismo candado que MisCuotas/MisAsistencias.
    [Authorize(Roles = "Familia,Alumno")]
    public class AsistenteController : Controller
    {
        private readonly AdministracionDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AsistenteService _asistenteService;

        public AsistenteController(
            AdministracionDbContext context,
            UserManager<ApplicationUser> userManager,
            AsistenteService asistenteService)
        {
            _context = context;
            _userManager = userManager;
            _asistenteService = asistenteService;
        }

        public class ConsultaRequest
        {
            public string Mensaje { get; set; } = string.Empty;
        }

        [HttpPost]
        public async Task<IActionResult> Consultar([FromBody] ConsultaRequest request)
        {
            var userId = _userManager.GetUserId(User);
            var esFamilia = User.IsInRole("Familia");

            var tools = esFamilia ? ToolsFamilia : ToolsAlumno;
            var systemPrompt = esFamilia
                ? "Sos el asistente del portal familiar. Respondé en español rioplatense, tono cordial y breve. Solo podés hablar de las cuotas y pagos de los alumnos de esta familia."
                : "Sos el asistente de asistencia del alumno. Respondé en español rioplatense, tono cordial y breve. Solo podés hablar de la asistencia del propio alumno logueado, nunca de compañeros.";

            var mensajes = new List<object>
            {
                new { role = "user", content = request.Mensaje }
            };

            // Primera llamada: el modelo decide si responde directo o pide ejecutar una tool.
            using var respuesta = await _asistenteService.EnviarMensajeAsync(mensajes, tools, systemPrompt);
            var content = respuesta.RootElement.GetProperty("content");

            string? toolUseId = null;
            string? toolName = null;
            JsonElement toolInput = default;
            var textoDirecto = "";

            foreach (var bloque in content.EnumerateArray())
            {
                var tipo = bloque.GetProperty("type").GetString();
                if (tipo == "text")
                {
                    textoDirecto += bloque.GetProperty("text").GetString();
                }
                else if (tipo == "tool_use")
                {
                    toolUseId = bloque.GetProperty("id").GetString();
                    toolName = bloque.GetProperty("name").GetString();
                    toolInput = bloque.GetProperty("input");
                }
            }

            // El modelo respondió directo, sin necesitar datos: devolvemos ya.
            if (toolName == null)
            {
                return Json(new { respuesta = textoDirecto });
            }

            // Ejecutamos la tool pedida CONTRA LA BASE REAL, validando siempre el dueño.
            object resultadoTool;
            try
            {
                resultadoTool = esFamilia
                    ? await EjecutarToolFamilia(toolName, toolInput, userId!)
                    : await EjecutarToolAlumno(toolName, toolInput, userId!);
            }
            catch (UnauthorizedAccessException)
            {
                return Json(new { respuesta = "No encontré ese dato asociado a tu cuenta." });
            }

            // Segunda llamada: le devolvemos el resultado real para que arme la respuesta final.
            mensajes.Add(new
            {
                role = "assistant",
                content = content.EnumerateArray().Select(b => (object)JsonSerializer.Deserialize<object>(b.GetRawText())!).ToList()
            });
            mensajes.Add(new
            {
                role = "user",
                content = new object[]
                {
                    new
                    {
                        type = "tool_result",
                        tool_use_id = toolUseId,
                        content = JsonSerializer.Serialize(resultadoTool)
                    }
                }
            });

            using var respuestaFinal = await _asistenteService.EnviarMensajeAsync(mensajes, tools, systemPrompt);
            var textoFinal = respuestaFinal.RootElement.GetProperty("content")
                .EnumerateArray()
                .Where(b => b.GetProperty("type").GetString() == "text")
                .Select(b => b.GetProperty("text").GetString())
                .FirstOrDefault() ?? "";

            return Json(new { respuesta = textoFinal });
        }

        // ---------- Tools: rol Familia ----------

        private static readonly object[] ToolsFamilia = new object[]
        {
            new
            {
                name = "ConsultarEstadoCuenta",
                description = "Consulta las cuotas pendientes y vencidas de un alumno de la familia logueada",
                input_schema = new
                {
                    type = "object",
                    properties = new { alumnoId = new { type = "integer", description = "ID del alumno" } },
                    required = new[] { "alumnoId" }
                }
            },
            new
            {
                name = "GenerarLinkDePago",
                description = "Devuelve el saldo pendiente de una cuota y la URL para confirmar el pago. NO ejecuta el pago: el usuario debe confirmar con un click.",
                input_schema = new
                {
                    type = "object",
                    properties = new { cuotaId = new { type = "integer", description = "ID de la cuota a pagar" } },
                    required = new[] { "cuotaId" }
                }
            },
            new
            {
                name = "ConsultarHistorialPagos",
                description = "Lista los pagos aprobados de un alumno, con fecha y monto",
                input_schema = new
                {
                    type = "object",
                    properties = new { alumnoId = new { type = "integer" } },
                    required = new[] { "alumnoId" }
                }
            }
        };

        private async Task<object> EjecutarToolFamilia(string toolName, JsonElement input, string userId)
        {
            switch (toolName)
            {
                case "ConsultarEstadoCuenta":
                {
                    var alumnoId = input.GetProperty("alumnoId").GetInt32();
                    var alumno = await _context.Alumnos
                        .FirstOrDefaultAsync(a => a.Id == alumnoId && a.FamiliaUserId == userId);
                    if (alumno == null) throw new UnauthorizedAccessException();

                    var cuotas = await _context.Cuotas
                        .Include(c => c.Pagos)
                        .Where(c => c.AlumnoId == alumnoId && c.Activo
                            && (c.Estado == EstadoCuota.Pendiente || c.Estado == EstadoCuota.Vencida || c.Estado == EstadoCuota.Parcial))
                        .OrderBy(c => c.FechaVencimiento)
                        .Select(c => new { c.Id, c.Mes, c.Anio, c.Estado, c.SaldoPendiente, c.FechaVencimiento })
                        .ToListAsync();

                    return new { alumno = $"{alumno.Nombre} {alumno.Apellido}", cuotas };
                }

                case "GenerarLinkDePago":
                {
                    var cuotaId = input.GetProperty("cuotaId").GetInt32();
                    var cuota = await _context.Cuotas
                        .Include(c => c.Alumno)
                        .Include(c => c.Pagos)
                        .FirstOrDefaultAsync(c => c.Id == cuotaId && c.Alumno.FamiliaUserId == userId);
                    if (cuota == null) throw new UnauthorizedAccessException();

                    return new
                    {
                        cuotaId = cuota.Id,
                        saldoPendiente = cuota.SaldoPendiente,
                        urlConfirmacion = Url.Action("Pagar", "Pagos", new { cuotaId = cuota.Id })
                    };
                }

                case "ConsultarHistorialPagos":
                {
                    var alumnoId = input.GetProperty("alumnoId").GetInt32();
                    var alumno = await _context.Alumnos
                        .FirstOrDefaultAsync(a => a.Id == alumnoId && a.FamiliaUserId == userId);
                    if (alumno == null) throw new UnauthorizedAccessException();

                    var pagos = await _context.Pagos
                        .Where(p => p.Cuota.AlumnoId == alumnoId && p.Estado == EstadoPago.Aprobado)
                        .OrderByDescending(p => p.Fecha)
                        .Select(p => new { p.Monto, p.Fecha, cuota = $"{p.Cuota.Mes}/{p.Cuota.Anio}" })
                        .ToListAsync();

                    return new { pagos };
                }

                default:
                    throw new InvalidOperationException($"Tool desconocida: {toolName}");
            }
        }

        // ---------- Tools: rol Alumno ----------

        private static readonly object[] ToolsAlumno = new object[]
        {
            new
            {
                name = "ConsultarMisFaltas",
                description = "Consulta el total de faltas y % de presentismo del alumno logueado en sus cursos, en un período reciente",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        periodo = new
                        {
                            type = "string",
                            @enum = new[] { "semana", "mes", "todo" },
                            description = "Rango a consultar: semana actual, mes actual, o todo el historial"
                        }
                    },
                    required = new[] { "periodo" }
                }
            }
        };

        private async Task<object> EjecutarToolAlumno(string toolName, JsonElement input, string userId)
        {
            switch (toolName)
            {
                case "ConsultarMisFaltas":
                {
                    var periodo = input.GetProperty("periodo").GetString() ?? "semana";
                    var hoy = DateTime.Today;

                    DateTime? inicio = periodo switch
                    {
                        "mes" => new DateTime(hoy.Year, hoy.Month, 1),
                        "todo" => null,
                        _ => hoy.AddDays(-((int)hoy.DayOfWeek + 6) % 7)
                    };

                    var inscripciones = await _context.Inscripciones
                        .Include(i => i.Curso)
                        .Include(i => i.Asistencias)
                        .Where(i => i.Alumno.AlumnoUserId == userId && i.Activo)
                        .ToListAsync();

                    var resultado = inscripciones.Select(i =>
                    {
                        var asistenciasDelPeriodo = inicio == null
                            ? i.Asistencias
                            : i.Asistencias.Where(a => a.Fecha >= inicio.Value).ToList();

                        return new
                        {
                            curso = $"{i.Curso.Nivel} {i.Curso.GradoAnio} - {i.Curso.Turno}",
                            totalFaltas = AsistenciaCalculadora.CalcularTotalFaltas(asistenciasDelPeriodo, i.Curso),
                            presentismo = AsistenciaCalculadora.CalcularPresentismo(asistenciasDelPeriodo, i.Curso)
                        };
                    });

                    return new { periodo, cursos = resultado };
                }

                default:
                    throw new InvalidOperationException($"Tool desconocida: {toolName}");
            }
        }
    }
}
