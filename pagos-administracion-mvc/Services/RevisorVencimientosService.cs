using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Services
{
    public class RevisorVencimientosService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<RevisorVencimientosService> _logger;

        public RevisorVencimientosService(IServiceProvider services, ILogger<RevisorVencimientosService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _services.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AdministracionDbContext>();
                    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

                    // 1. Marcar vencidas
                    var vencidas = await context.Cuotas
                        .Include(c => c.Alumno)
                        .Where(c => c.Estado == EstadoCuota.Pendiente && c.FechaVencimiento < DateTime.Today)
                        .ToListAsync(stoppingToken);

                    foreach (var cuota in vencidas)
                        cuota.Estado = EstadoCuota.Vencida;

                    if (vencidas.Any())
                        await context.SaveChangesAsync(stoppingToken);

                    // 2. Avisar 3 días antes del vencimiento (una sola vez -- ver nota abajo)
                    var proximasAVencer = await context.Cuotas
                        .Include(c => c.Alumno)
                        .Where(c => c.Estado == EstadoCuota.Pendiente && c.FechaVencimiento == DateTime.Today.AddDays(3))
                        .ToListAsync(stoppingToken);

                    foreach (var cuota in proximasAVencer.Concat(vencidas))
                    {
                        if (cuota.Alumno.FamiliaUserId == null) continue;

                        var familia = await userManager.FindByIdAsync(cuota.Alumno.FamiliaUserId);
                        if (familia?.Email == null) continue;

                        var asunto = cuota.Estado == EstadoCuota.Vencida
                            ? $"Cuota vencida - {cuota.Alumno.Nombre} {cuota.Alumno.Apellido}"
                            : $"Recordatorio: cuota próxima a vencer - {cuota.Alumno.Nombre} {cuota.Alumno.Apellido}";

                        var cuerpo = $"<p>La cuota de {cuota.Mes}/{cuota.Anio} por {cuota.Monto:C} " +
                                     $"{(cuota.Estado == EstadoCuota.Vencida ? "venció el" : "vence el")} " +
                                     $"{cuota.FechaVencimiento:dd/MM/yyyy}. Ingresá al portal para abonarla.</p>";

                        await emailService.EnviarAsync(familia.Email, asunto, cuerpo);
                    }
                }

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }

    }
}