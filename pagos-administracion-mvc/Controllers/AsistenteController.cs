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
        private readonly ConversacionAsistenteStore _historialStore;

        public AsistenteController(
            AdministracionDbContext context,
            UserManager<ApplicationUser> userManager,
            AsistenteService asistenteService,
            ConversacionAsistenteStore historialStore)
        {
            _context = context;
            _userManager = userManager;
            _asistenteService = asistenteService;
            _historialStore = historialStore;
        }

        public class ConsultaRequest
        {
            public string Mensaje { get; set; } = string.Empty;
        }

        [HttpPost]
        public async Task<IActionResult> Consultar([FromBody] ConsultaRequest request)
        {
            var userId = _userManager.GetUserId(User)!;
            var esFamilia = User.IsInRole("Familia");
            var tools = esFamilia ? ToolsFamilia : ToolsAlumno;

            var systemPrompt = esFamilia
    ? "Sos el asistente del portal escolar. Respondé en español rioplatense, máximo 1 o 2 oraciones. Si te piden ver cuotas o pagos, preguntá el nombre o DNI del alumno. No pidas el ID numérico. También podés informar avisos, fechas importantes del colegio y el valor vigente (o cambios) de los aranceles cuando te lo pidan."
    : "Sos el asistente del portal escolar. Respondé en español rioplatense, máximo 1 o 2 oraciones. Podés informar sobre tu asistencia y sobre avisos o fechas importantes del colegio. No ofrezcas ayuda extra fuera de esos temas.";

            var mensajeUsuario = new { role = "user", content = request.Mensaje };
            var mensajes = new List<object> { new { role = "system", content = systemPrompt } };

            // Recuperamos el historial de turnos previos de este usuario en memoria
            var historial = _historialStore.Obtener(userId);
            foreach (var turno in historial)
            {
                foreach (var m in turno)
                {
                    mensajes.Add(JsonSerializer.Deserialize<object>(m)!);
                }
            }

            mensajes.Add(mensajeUsuario);

            try
            {
                using var respuesta = await _asistenteService.EnviarMensajeAsync(mensajes, tools);
                var choice = respuesta.RootElement.GetProperty("choices")[0].GetProperty("message");

                string? toolUseId = null;
                string? toolName = null;
                JsonElement toolInput = default;

                var textoDirecto = choice.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String
                    ? contentProp.GetString() : "";

                if (choice.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.GetArrayLength() > 0)
                {
                    var firstCall = toolCalls[0];
                    toolUseId = firstCall.GetProperty("id").GetString();
                    toolName = firstCall.GetProperty("function").GetProperty("name").GetString();
                    var argsTexto = firstCall.GetProperty("function").GetProperty("arguments").GetString();
                    toolInput = string.IsNullOrWhiteSpace(argsTexto)
                        ? JsonDocument.Parse("{}").RootElement
                        : JsonDocument.Parse(argsTexto).RootElement;
                }

                if (toolName == null)
                {
                    // Turno simple sin herramientas: se guarda user + assistant
                    _historialStore.AgregarTurno(userId, new List<string>
                    {
                        JsonSerializer.Serialize(mensajeUsuario),
                        JsonSerializer.Serialize(new { role = "assistant", content = textoDirecto })
                    });

                    return Json(new { respuesta = textoDirecto });
                }

                object resultadoTool;
                try
                {
                    resultadoTool = esFamilia
                        ? await EjecutarToolFamilia(toolName, toolInput, userId)
                        : await EjecutarToolAlumno(toolName, toolInput, userId);
                }
                catch (UnauthorizedAccessException)
                {
                    var msgError = "No encontré ese dato asociado a tu cuenta.";
                    _historialStore.AgregarTurno(userId, new List<string>
                    {
                        JsonSerializer.Serialize(mensajeUsuario),
                        JsonSerializer.Serialize(new { role = "assistant", content = msgError })
                    });
                    return Json(new { respuesta = msgError });
                }

                var mensajeAssistantConTool = JsonSerializer.Deserialize<object>(choice.GetRawText())!;
                var mensajeTool = new
                {
                    role = "tool",
                    tool_call_id = toolUseId,
                    content = JsonSerializer.Serialize(resultadoTool)
                };

                mensajes.Add(mensajeAssistantConTool);
                mensajes.Add(mensajeTool);

                using var respuestaFinal = await _asistenteService.EnviarMensajeAsync(mensajes, tools);
                var textoFinal = respuestaFinal.RootElement.GetProperty("choices")[0]
                    .GetProperty("message").GetProperty("content").GetString() ?? "";

                // Turno con tool call: se guarda user + assistant(tools) + tool result + assistant final
                _historialStore.AgregarTurno(userId, new List<string>
                {
                    JsonSerializer.Serialize(mensajeUsuario),
                    JsonSerializer.Serialize(mensajeAssistantConTool),
                    JsonSerializer.Serialize(mensajeTool),
                    JsonSerializer.Serialize(new { role = "assistant", content = textoFinal })
                });

                return Json(new { respuesta = textoFinal });
            }
            catch (Exception ex)
            {
                return Json(new { respuesta = $"Ocurrió un problema de conexión temporal. Intentá nuevamente en unos segundos. ({ex.Message})" });
            }
        }

        [HttpPost]
        public IActionResult Reiniciar()
        {
            var userId = _userManager.GetUserId(User)!;
            _historialStore.Reiniciar(userId);
            return Ok();
        }

        // ---------- Tools: rol Familia (Formato OpenAI / Groq) ----------

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
                    description = "Devuelve la URL de pago. Requiere el ID numérico de la cuota.",
                    parameters = new
                    {
                        type = "object",
                        properties = new { cuotaId = new { type = "integer", description = "ID numérico de la cuota" } },
                        required = new[] { "cuotaId" }
                    }
                }
            },
            new
{
            type = "function",
            function = new
            {
                name = "ConsultarAvisos",
                description = "Devuelve los últimos avisos, novedades y fechas importantes publicados por el colegio (actos, suspensión de clases, reuniones, cambios de horario, etc). Usala cuando pregunten por novedades, avisos, o próximos eventos del colegio.",
                parameters = new
            {
                type = "object",
            properties = new
            {
                tipo = new
                {
                    type = "string",
                    @enum = new[] { "Importante", "Calendario", "Aviso", "Todos" },
                    description = "Filtrar por tipo: Importante (urgente), Calendario (fechas/eventos), Aviso (general). Usar 'Todos' si no se especifica nada."
                }
            },
            required = new string[] { }
                    }
                }
    
            },
            new
{
    type = "function",
    function = new
    {
        name = "ConsultarAranceles",
        description = "Consulta el valor vigente del arancel (cuota) por nivel educativo, con el desglose de conceptos, y si hubo un cambio respecto al arancel anterior. Usala cuando pregunten cuánto sale la cuota o si hubo un aumento.",
        parameters = new
        {
            type = "object",
            properties = new
            {
                nivel = new
                {
                    type = "string",
                    @enum = new[] { "Primaria", "Secundaria" },
                    description = "Nivel educativo a consultar. Si no se especifica, se devuelven todos los niveles de los alumnos de la familia."
                }
            },
            required = new string[] { }
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
                        if (!input.TryGetProperty("nombreODni", out var propODni)) throw new UnauthorizedAccessException();
                        var nombreODni = propODni.GetString()?.Trim().ToLower() ?? "";

                        var alumno = await _context.Alumnos
                            .FirstOrDefaultAsync(a => a.FamiliaUserId == userId &&
                                (a.Dni == nombreODni || a.Nombre.ToLower().Contains(nombreODni) || a.Apellido.ToLower().Contains(nombreODni) ||
                                 (a.Nombre.ToLower() + " " + a.Apellido.ToLower()).Contains(nombreODni)));

                        if (alumno == null) throw new UnauthorizedAccessException();

                        var cuotas = await _context.Cuotas
                            .Where(c => c.AlumnoId == alumno.Id && c.Activo && (c.Estado == EstadoCuota.Pendiente || c.Estado == EstadoCuota.Vencida || c.Estado == EstadoCuota.Parcial))
                            .OrderBy(c => c.FechaVencimiento)
                            .Select(c => new { c.Id, c.Mes, c.Anio, c.SaldoPendiente, c.FechaVencimiento })
                            .ToListAsync();

                        return new { alumno = $"{alumno.Nombre} {alumno.Apellido}", cuotas };
                    }
                case "GenerarLinkDePago":
                    {
                        if (!input.TryGetProperty("cuotaId", out var cuotaIdProp)) throw new UnauthorizedAccessException();
                        var cuotaId = cuotaIdProp.ValueKind == JsonValueKind.Number ? cuotaIdProp.GetInt32() : int.Parse(cuotaIdProp.GetString()!);

                        var cuota = await _context.Cuotas.Include(c => c.Alumno)
                            .FirstOrDefaultAsync(c => c.Id == cuotaId && c.Alumno.FamiliaUserId == userId);

                        if (cuota == null) throw new UnauthorizedAccessException();

                        return new
                        {
                            saldoPendiente = cuota.SaldoPendiente,
                            urlConfirmacion = Url.Action("Confirmar", "Pagos", new { cuotaId = cuota.Id })
                        };
                    }
                case "ConsultarAvisos":
                    return await EjecutarConsultarAvisos(input);

                case "ConsultarAranceles":
                    {
                        NivelEducativo? nivelFiltro = null;
                        if (input.ValueKind == JsonValueKind.Object && input.TryGetProperty("nivel", out var nivelProp)
                            && Enum.TryParse<NivelEducativo>(nivelProp.GetString(), true, out var parsed))
                        {
                            nivelFiltro = parsed;
                        }

                        var niveles = nivelFiltro.HasValue
                            ? new[] { nivelFiltro.Value }
                            : await _context.Alumnos
                                .Where(a => a.FamiliaUserId == userId && a.Activo)
                                .Select(a => a.Nivel)
                                .Distinct()
                                .ToArrayAsync();

                        if (niveles.Length == 0) throw new UnauthorizedAccessException();

                        var resultado = new List<object>();
                        foreach (var nivel in niveles)
                        {
                            var vigentes = await _context.ArancelesNivel
                                .Where(a => a.Nivel == nivel && a.Activo && a.VigenteDesde <= DateTime.Today)
                                .OrderByDescending(a => a.VigenteDesde)
                                .Take(2)
                                .ToListAsync();

                            if (vigentes.Count == 0) continue;

                            var actual = vigentes[0];
                            var anterior = vigentes.Count > 1 ? vigentes[1] : null;

                            resultado.Add(new
                            {
                                nivel = nivel.ToString(),
                                vigenteDesde = actual.VigenteDesde,
                                total = actual.TotalConBonificacion,
                                cuotaSinBonificacion = actual.CuotaReal,
                                bonificacionPagoATiempo = actual.BonificacionPagoATiempo,
                                cambio = anterior == null ? null : new
                                {
                                    totalAnterior = anterior.TotalConBonificacion,
                                    diferencia = actual.TotalConBonificacion - anterior.TotalConBonificacion,
                                    vigenteDesdeAnterior = anterior.VigenteDesde
                                }
                            });
                        }

                        return new { aranceles = resultado };
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
                type = "function",
                function = new
                {
                    name = "ConsultarMisFaltas",
                    description = "Consulta faltas del alumno logueado.",
                    parameters = new
                    {
                        type = "object",
                        properties = new { periodo = new { type = "string", @enum = new[] { "semana", "mes", "todo" } } },
                        required = new[] { "periodo" }
                    }
                }
            },
            new
{
    type = "function",
    function = new
    {
        name = "ConsultarAvisos",
        description = "Devuelve los últimos avisos, novedades y fechas importantes publicados por el colegio (actos, suspensión de clases, reuniones, cambios de horario, etc). Usala cuando pregunten por novedades, avisos, o próximos eventos del colegio.",
        parameters = new
        {
            type = "object",
            properties = new
            {
                tipo = new
                {
                    type = "string",
                    @enum = new[] { "Importante", "Calendario", "Aviso", "Todos" },
                    description = "Filtrar por tipo: Importante (urgente), Calendario (fechas/eventos), Aviso (general). Usar 'Todos' si no se especifica nada."
                }
            },
            required = new string[] { }
        }
    }
}
        };

        private async Task<object> EjecutarToolAlumno(string toolName, JsonElement input, string userId)
        {
            switch (toolName)
            {
                case "ConsultarAvisos":
                    return await EjecutarConsultarAvisos(input);
                default:
                    throw new InvalidOperationException($"Tool desconocida: {toolName}");
            }
            // Implementación simplificada para mantener la compilación
            return new { mensaje = "Consulta de faltas ejecutada." };
        }

        private async Task<object> EjecutarConsultarAvisos(JsonElement input)
        {
            string? tipoTexto = input.ValueKind == JsonValueKind.Object && input.TryGetProperty("tipo", out var tipoProp)
                ? tipoProp.GetString()
                : null;

            var query = _context.Avisos.Where(a => a.Activo);

            if (!string.IsNullOrEmpty(tipoTexto) && tipoTexto != "Todos"
                && Enum.TryParse<TipoAviso>(tipoTexto, true, out var tipoFiltro))
            {
                query = query.Where(a => a.Tipo == tipoFiltro);
            }

            var avisos = await query
                .OrderByDescending(a => a.FechaPublicacion)
                .Take(5)
                .Select(a => new { a.Titulo, a.Descripcion, tipo = a.Tipo.ToString(), a.FechaPublicacion })
                .ToListAsync();

            return new { avisos };
        }
    }
}