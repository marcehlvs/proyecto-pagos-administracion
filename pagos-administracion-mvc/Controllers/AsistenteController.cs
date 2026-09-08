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
            var tools = esFamilia ? ToolsFamilia : ToolsAlumno;

            var systemPrompt = esFamilia
                ? "Sos el asistente del portal escolar. Respondé en español rioplatense, máximo 1 o 2 oraciones. Si te piden ver cuotas o pagos, preguntá el nombre o DNI del alumno. No pidas el ID numérico."
                : "Sos el asistente del portal escolar. Respondé en español rioplatense, máximo 1 o 2 oraciones. Solo informá sobre tu asistencia. No ofrezcas ayuda extra.";

            // OpenAI/Groq inserta el system prompt como primer mensaje
            var mensajes = new List<object>
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = request.Mensaje }
            };

            try
            {
                using var respuesta = await _asistenteService.EnviarMensajeAsync(mensajes, tools);
                var choice = respuesta.RootElement.GetProperty("choices")[0].GetProperty("message");

                string? toolUseId = null;
                string? toolName = null;
                JsonElement toolInput = default;

                var textoDirecto = choice.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String
                    ? contentProp.GetString() : "";

                // OpenAI envía los argumentos de la tool como un string JSON que hay que parsear
                if (choice.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.GetArrayLength() > 0)
                {
                    var firstCall = toolCalls[0];
                    toolUseId = firstCall.GetProperty("id").GetString();
                    toolName = firstCall.GetProperty("function").GetProperty("name").GetString();
                    toolInput = JsonDocument.Parse(firstCall.GetProperty("function").GetProperty("arguments").GetString()!).RootElement;
                }

                if (toolName == null)
                {
                    return Json(new { respuesta = textoDirecto });
                }

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

                // Agregamos la respuesta del asistente (el llamado a la tool) y el resultado real
                mensajes.Add(JsonSerializer.Deserialize<object>(choice.GetRawText())!);
                mensajes.Add(new
                {
                    role = "tool",
                    tool_call_id = toolUseId,
                    content = JsonSerializer.Serialize(resultadoTool)
                });

                using var respuestaFinal = await _asistenteService.EnviarMensajeAsync(mensajes, tools);
                var textoFinal = respuestaFinal.RootElement.GetProperty("choices")[0]
                    .GetProperty("message").GetProperty("content").GetString() ?? "";

                return Json(new { respuesta = textoFinal });
            }
            catch (Exception ex)
            {
                // Dejo expuesto el error técnico para que veas si la clave de Groq agarra bien de entrada
                return Json(new { respuesta = $"Error técnico para depurar: {ex.Message}" });
            }
        }

        // ---------- Tools: rol Familia (Formato OpenAI) ----------

        private static readonly object[] ToolsFamilia = new object[]
        {
            new
            {
                type = "function",
                function = new
                {
                    name = "ConsultarEstadoCuenta",
                    description = "Consulta las cuotas pendientes y vencidas de un alumno. Requiere nombre o DNI.",
                    parameters = new
                    {
                        type = "object",
                        properties = new { nombreODni = new { type = "string", description = "Nombre, apellido o DNI del alumno" } },
                        required = new[] { "nombreODni" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "GenerarLinkDePago",
                    description = "Devuelve el saldo pendiente de una cuota y la URL para confirmar el pago. NO ejecuta el pago: el usuario debe confirmar con un click.",
                    parameters = new
                    {
                        type = "object",
                        properties = new { cuotaId = new { type = "integer", description = "ID de la cuota a pagar" } },
                        required = new[] { "cuotaId" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "ConsultarHistorialPagos",
                    description = "Lista los pagos aprobados de un alumno. Requiere nombre o DNI.",
                    parameters = new
                    {
                        type = "object",
                        properties = new { nombreODni = new { type = "string" } },
                        required = new[] { "nombreODni" }
                    }
                }
            }
        };

        private async Task<object> EjecutarToolFamilia(string toolName, JsonElement input, string userId)
        {
            switch (toolName)
            {
                case "ConsultarEstadoCuenta":
                    {
                        if (!input.TryGetProperty("nombreODni", out var propODni))
                            throw new UnauthorizedAccessException();

                        var nombreODni = propODni.GetString()?.Trim().ToLower() ?? "";

                        var alumno = await _context.Alumnos
                            .FirstOrDefaultAsync(a => a.FamiliaUserId == userId &&
                                (a.Dni == nombreODni ||
                                 a.Nombre.ToLower().Contains(nombreODni) ||
                                 a.Apellido.ToLower().Contains(nombreODni) ||
                                 (a.Nombre.ToLower() + " " + a.Apellido.ToLower()).Contains(nombreODni) ||
                                 (a.Apellido.ToLower() + ", " + a.Nombre.ToLower()).Contains(nombreODni)));

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
                        if (!input.TryGetProperty("cuotaId", out var cuotaIdProp))
                            throw new UnauthorizedAccessException();

                        var cuotaId = cuotaIdProp.ValueKind == JsonValueKind.Number ? cuotaIdProp.GetInt32() : int.Parse(cuotaIdProp.GetString()!);

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
                        if (!input.TryGetProperty("nombreODni", out var propODni))
                            throw new UnauthorizedAccessException();

                        var nombreODni = propODni.GetString()?.Trim().ToLower() ?? "";

                        var alumno = await _context.Alumnos
                            .FirstOrDefaultAsync(a => a.FamiliaUserId == userId &&
                                (a.Dni == nombreODni ||
                                 a.Nombre.ToLower().Contains(nombreODni) ||
                                 a.Apellido.ToLower().Contains(nombreODni) ||
                                 (a.Nombre.ToLower() + " " + a.Apellido.ToLower()).Contains(nombreODni) ||
                                 (a.Apellido.ToLower() + ", " + a.Nombre.ToLower()).Contains(nombreODni)));

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

        // ---------- Tools: rol Alumno (Formato OpenAI) ----------

        private static readonly object[] ToolsAlumno = new object[]
        {
            new
            {
                type = "function",
                function = new
                {
                    name = "ConsultarMisFaltas",
                    description = "Consulta el total de faltas y % de presentismo del alumno logueado en sus cursos, en un período reciente",
                    parameters = new
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
            }
        };

        private async Task<object> EjecutarToolAlumno(string toolName, JsonElement input, string userId)
        {
            switch (toolName)
            {
                case "ConsultarMisFaltas":
                    {
                        var periodo = input.TryGetProperty("periodo", out var prop) ? prop.GetString() ?? "semana" : "semana";
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