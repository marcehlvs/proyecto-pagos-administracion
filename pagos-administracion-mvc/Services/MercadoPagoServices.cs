using MercadoPago.Client.Preference;
using MercadoPago.Resource.Preference;
using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Services
{
    public class MercadoPagoService
    {
        private readonly IConfiguration _config;

        public MercadoPagoService(IConfiguration config)
        {
            _config = config;
        }

        public async Task<Preference> CrearPreferenciaAsync(Pago pago, Cuota cuota)
        {
            var baseUrl = _config["AppBaseUrl"]; // ej: https://xxxxx.ngrok-free.app

            var request = new PreferenceRequest
            {
                Items = new List<PreferenceItemRequest>
                {
                    new PreferenceItemRequest
                    {
                        Title = $"Cuota {cuota.Mes}/{cuota.Anio} - {cuota.Alumno.Apellido} {cuota.Alumno.Nombre}",
                        Quantity = 1,
                        CurrencyId = "ARS",
                        UnitPrice = pago.Monto
                    }
                },
                BackUrls = new PreferenceBackUrlsRequest
                {
                    Success = $"{baseUrl}/Pagos/PagoExitoso",
                    Failure = $"{baseUrl}/Pagos/PagoFallido",
                    Pending = $"{baseUrl}/Pagos/PagoPendiente"
                },
                AutoReturn = "approved",
                NotificationUrl = $"{baseUrl}/api/mercadopago/webhook",
                ExternalReference = pago.Id.ToString()
            };

            var client = new PreferenceClient();
            return await client.CreateAsync(request);
        }
    }
}
