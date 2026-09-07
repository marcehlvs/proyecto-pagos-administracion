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

            // Gemini espera que las declaraciones de funciones estén dentro de un array de functionDeclarations
            var tools = new object[]
            {
                new { functionDeclarations = esFamilia ? ToolsFamilia : ToolsAlumno }
            };

            var systemPrompt = esFamilia
    ? "Sos el asistente del portal escolar. Respondé en español rioplatense, máximo 1 o 2 oraciones. Si te piden ver cuotas o pagos, preguntá el nombre o DNI del alumno. No pidas el ID numérico."
    : "Sos el asistente del portal escolar. Respondé en español rioplatense, máximo 1 o 2 oraciones. Solo informá sobre tu asistencia. No ofrezcas ayuda extra.";
            // Gemini estructura el historial con "parts"
            var mensajes = new List<object>
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = request.Mensaje } }
                }
            };

            try
            {
                // Primera llamada
                using var respuesta = await _asistenteService.EnviarMensajeAsync(mensajes, tools, systemPrompt);

                var candidates = respuesta.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() == 0)
                    throw new Exception("Respuesta vacía de Gemini");

                var parts = candidates[0].GetProperty("content").GetProperty("parts");

                string? toolName = null;
                JsonElement toolInput = default;
                var textoDirecto = "";

                // Analizamos si la respuesta es texto directo o un pedido para usar una herramienta
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var textProp))
                    {
                        textoDirecto += textProp.GetString();
                    }
                    else if (part.TryGetProperty("functionCall", out var funcCall))
                    {
                        toolName = funcCall.GetProperty("name").GetString();
                        toolInput = funcCall.GetProperty("args");
                    }
                }

                // El modelo respondió directo
                if (toolName == null)
                {
                    return Json(new { respuesta = textoDirecto });
                }

                // Ejecución local en base de datos
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

                // Segunda llamada: reconstruimos la parte del asistente y agregamos el functionResponse
                mensajes.Add(new
                {
                    role = "model",
                    parts = parts.EnumerateArray().Select(p => (object)JsonSerializer.Deserialize<object>(p.GetRawText())!).ToList()
                });

                mensajes.Add(new
                {
                    role = "user",
                    parts = new[]
                    {
                        new
                        {
                            functionResponse = new
                            {
                                name = toolName,
                                response = resultadoTool
                            }
                        }
                    }
                });

                using var respuestaFinal = await _asistenteService.EnviarMensajeAsync(mensajes, tools, systemPrompt);
                var finalCandidates = respuestaFinal.RootElement.GetProperty("candidates");
                var finalParts = finalCandidates[0].GetProperty("content").GetProperty("parts");

                var textoFinal = finalParts.EnumerateArray()
                    .Where(p => p.TryGetProperty("text", out _))
                    .Select(p => p.GetProperty("text").GetString())
                    .FirstOrDefault() ?? "";

                return Json(new { respuesta = textoFinal });
            }
            catch (Exception)
            {
                return Json(new { respuesta = "El sistema está procesando muchas consultas en este momento. Por favor, intentá de nuevo en un minuto." });
            }
        }

        // ---------- Tools: rol Familia ----------
        // Ajustado al formato OpenAPI que requiere Gemini (parameters, types en mayúscula)

        private static readonly object[] ToolsFamilia = new object[]
{
    new
    {
        name = "ConsultarEstadoCuenta",
        description = "Consulta las cuotas pendientes y vencidas de un alumno. Requiere nombre o DNI.",
        parameters = new
        {
            type = "OBJECT",
            properties = new { nombreODni = new { type = "STRING", description = "Nombre, apellido o DNI del alumno" } },
            required = new[] { "nombreODni" }
        }
    },
    new
    {
        name = "GenerarLinkDePago",
        description = "Devuelve el saldo pendiente de una cuota y la URL para confirmar el pago. NO ejecuta el pago: el usuario debe confirmar con un click.",
        parameters = new
        {
            type = "OBJECT",
            properties = new { cuotaId = new { type = "INTEGER", description = "ID de la cuota a pagar" } },
            required = new[] { "cuotaId" }
        }
    },
    new
    {
        name = "ConsultarHistorialPagos",
        description = "Lista los pagos aprobados de un alumno. Requiere nombre o DNI.",
        parameters = new
        {
            type = "OBJECT",
            properties = new { nombreODni = new { type = "STRING" } },
            required = new[] { "nombreODni" }
        }
    }
};

        private async Task<object> EjecutarToolFamilia(string toolName, JsonElement input, string userId)
        {
            switch (toolName)
            {
                case "ConsultarEstadoCuenta":
                    {
                        var nombreODni = input.GetProperty("nombreODni").GetString()?.Trim().ToLower() ?? "";

                        // El filtro a.FamiliaUserId == userId es el candado absoluto de seguridad.
                        var alumno = await _context.Alumnos
                            .FirstOrDefaultAsync(a => a.FamiliaUserId == userId &&
                                (a.Dni == nombreODni || a.Nombre.ToLower().Contains(nombreODni) || a.Apellido.ToLower().Contains(nombreODni)));

                        if (alumno == null) throw new UnauthorizedAccessException();

                        var cuotas = await _context.Cuotas
                            .Include(c => c.Pagos)
                            .Where(c => c.AlumnoId == alumno.Id && c.Activo
                                && (c.Estado == EstadoCuota.Pendiente || c.Estado == EstadoCuota.Vencida || c.Estado == EstadoCuota.Parcial))
                            .OrderBy(c => c.FechaVencimiento)
                            .Select(c => new { c.Id, c.Mes, c.Anio, c.Estado, c.SaldoPendiente, c.FechaVencimiento })
                            .ToListAsync();

                        return new { alumno = $"{alumno.Nombre} {alumno.Apellido}", cuotas };
                    }

                case "GenerarLinkDePago":
                    {
                        var cuotaIdProp = input.GetProperty("cuotaId");
                        var cuotaId = cuotaIdProp.ValueKind == JsonValueKind.Number ? cuotaIdProp.GetInt32() : int.Parse(cuotaIdProp.GetString()!);

                        // El candado acá se mantiene verificando que la cuota pertenezca a un alumno de este userId
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
                        var nombreODni = input.GetProperty("nombreODni").GetString()?.Trim().ToLower() ?? "";

                        var alumno = await _context.Alumnos
                            .FirstOrDefaultAsync(a => a.FamiliaUserId == userId &&
                                (a.Dni == nombreODni || a.Nombre.ToLower().Contains(nombreODni) || a.Apellido.ToLower().Contains(nombreODni)));

                        if (alumno == null) throw new UnauthorizedAccessException();

                        var pagos = await _context.Pagos
                            .Where(p => p.Cuota.AlumnoId == alumno.Id && p.Estado == EstadoPago.Aprobado)
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
                parameters = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        periodo = new
                        {
                            type = "STRING",
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