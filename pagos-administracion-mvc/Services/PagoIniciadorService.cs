using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Services
{
    public class PagoIniciadorService
    {
        private readonly AdministracionDbContext _context;
        private readonly MercadoPagoService _mpService;

        public PagoIniciadorService(AdministracionDbContext context, MercadoPagoService mpService)
        {
            _context = context;
            _mpService = mpService;
        }

        public async Task<string> IniciarPagoMercadoPagoAsync(Cuota cuota, string userId, string? userName)
        {
            var pago = new Pago
            {
                CuotaId = cuota.Id,
                Monto = cuota.SaldoPendiente,
                Fecha = DateTime.Now,
                Estado = EstadoPago.Pendiente,
                RegistradoPorUserId = userId,
                RegistradoPorNombre = userName,
                FechaRegistro = DateTime.Now
            };

            _context.Pagos.Add(pago);
            await _context.SaveChangesAsync();

            var preferencia = await _mpService.CrearPreferenciaAsync(pago, cuota);

            pago.MercadoPagoPreferenceId = preferencia.Id;
            await _context.SaveChangesAsync();

            return preferencia.InitPoint;
        }
    }
}