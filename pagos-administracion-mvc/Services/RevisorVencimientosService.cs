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

                    var vencidas = await context.Cuotas
                        .Where(c => c.Estado == EstadoCuota.Pendiente && c.FechaVencimiento < DateTime.Today)
                        .ToListAsync(stoppingToken);

                    if (vencidas.Any())
                    {
                        foreach (var cuota in vencidas)
                            cuota.Estado = EstadoCuota.Vencida;

                        await context.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Se marcaron {Cantidad} cuotas como vencidas.", vencidas.Count);
                    }
                }

                // Corre una vez por día.
                //TimeSpan.FromSeconds(30) // Para pruebas
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
})