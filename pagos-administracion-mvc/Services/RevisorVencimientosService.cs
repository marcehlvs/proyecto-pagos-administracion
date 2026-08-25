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

                    // 1. Marcar vencidas (Pendiente o Parcial: en ambos casos sigue habiendo saldo impago).
                    // Esto es idempotente por naturaleza: una vez que el Estado pasa a Vencida, esta query
                    // deja de traerla, así que no hace falta ningún flag acá.
                    var vencidas = await context.Cuotas
                        .Where(c => (c.Estado == EstadoCuota.Pendiente || c.Estado == EstadoCuota.Parcial) && c.FechaVencimiento < DateTime.Today)
                        .ToListAsync(stoppingToken);

                    foreach (var cuota in vencidas)
                        cuota.Estado = EstadoCuota.Vencida;

                    if (vencidas.Any())
                        await context.SaveChangesAsync(stoppingToken);

                    // 2. Avisar 3 días antes del vencimiento (una sola vez por cuota, para siempre).
                    // Usamos un rango en vez de "== Today.AddDays(3)" para no depender de pegarle al día
                    // exacto: si el job se saltea un día (reinicio, downtime), igual la agarra después,
                    // y el flag evita que se reenvíe una vez que ya se mandó.
                    var proximasAVencer = await context.Cuotas
                        .Include(c => c.Alumno)
                        .Include(c => c.Pagos)
                        .Where(c => (c.Estado == EstadoCuota.Pendiente || c.Estado == EstadoCuota.Parcial)
                                 && !c.AvisoProximoVencimientoEnviado
                                 && c.FechaVencimiento >= DateTime.Today
                                 && c.FechaVencimiento <= DateTime.Today.AddDays(3))
                        .ToListAsync(stoppingToken);

                    // 3. Avisar cuotas vencidas (una sola vez por cuota, para siempre). Se consulta por el
                    // flag, no por la lista "vencidas" de arriba: así, si una cuota vuelve a Parcial por un
                    // pago parcial y el job de "marcar vencidas" la re-marca como Vencida más adelante,
                    // el aviso NO se reenvía (ya se mandó una vez para esta cuota).
                    var vencidasSinAvisar = await context.Cuotas
                        .Include(c => c.Alumno)
                        .Include(c => c.Pagos)
                        .Where(c => c.Estado == EstadoCuota.Vencida && !c.AvisoVencidaEnviado)
                        .ToListAsync(stoppingToken);

                    foreach (var cuota in proximasAVencer.Concat(vencidasSinAvisar))
                    {
                        if (cuota.Alumno.FamiliaUserId == null) continue;

                        var familia = await userManager.FindByIdAsync(cuota.Alumno.FamiliaUserId);
                        if (familia?.Email == null) continue;

                        var asunto = cuota.Estado == EstadoCuota.Vencida
                            ? $"Cuota vencida - {cuota.Alumno.Nombre} {cuota.Alumno.Apellido}"
                            : $"Recordatorio: cuota próxima a vencer - {cuota.Alumno.Nombre} {cuota.Alumno.Apellido}";

                        var montoAviso = cuota.Estado == EstadoCuota.Parcial ? cuota.SaldoPendiente : cuota.Monto;
                        var cuerpo = $"<p>La cuota de {cuota.Mes}/{cuota.Anio} tiene un saldo de {montoAviso:C} " +
                                     $"{(cuota.Estado == EstadoCuota.Vencida ? "vencido, con vencimiento el" : "pendiente, que vence el")} " +
                                     $"{cuota.FechaVencimiento:dd/MM/yyyy}. Ingresá al portal para abonarla.</p>";

                        await emailService.EnviarAsync(familia.Email, asunto, cuerpo);

                        if (cuota.Estado == EstadoCuota.Vencida)
                            cuota.AvisoVencidaEnviado = true;
                        else
                            cuota.AvisoProximoVencimientoEnviado = true;
                    }

                    if (proximasAVencer.Any() || vencidasSinAvisar.Any())
                        await context.SaveChangesAsync(stoppingToken);
                }

                // Corre una vez por día, a una hora fija, en vez de contar 24hs desde que arrancó el
                // proceso: en hostings con reinicios frecuentes, "24hs desde el boot" hace que el chequeo
                // se dispare de nuevo cada vez que reinicia. Con hora fija, varios reinicios el mismo día
                // no generan corridas extra (y aunque generaran alguna, los flags de arriba ya evitan que
                // se reenvíe un mail).
                var ahora = DateTime.Now;
                var proximaCorrida = ahora.Date.AddHours(8);
                if (ahora >= proximaCorrida)
                    proximaCorrida = proximaCorrida.AddDays(1);

                await Task.Delay(proximaCorrida - ahora, stoppingToken);
            }
        }

    }
}