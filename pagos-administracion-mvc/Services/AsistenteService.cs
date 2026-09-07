using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace pagos_administracion_mvc.Services
{
    // Encapsula la comunicación con la API de Claude (mensajes + tool calling).
    // No conoce EF Core ni roles: solo arma el request, lo manda, y devuelve la
    // respuesta cruda para que el Controller decida qué tool ejecutar.
    // Mismo criterio que MercadoPagoService: la lógica de "hablar con el proveedor
    // externo" vive acá, separada del Controller.
    public class AsistenteService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        // Modelo corregido al nombre real que exige la API de Anthropic
        private const string Modelo = "claude-3-5-sonnet-20241022";

        public AsistenteService(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
            _apiKey = config["Claude:ApiKey"]
                ?? throw new InvalidOperationException("Falta configurar Claude:ApiKey en appsettings.");
        }

        // Envía el historial de mensajes + las tools disponibles (según el rol ya
        // resuelto por el Controller) y devuelve la respuesta cruda del modelo.
        public async Task<JsonDocument> EnviarMensajeAsync(
            List<object> mensajes,
            object[] tools,
            string systemPrompt)
        {
            var body = new
            {
                model = Modelo,
                max_tokens = 1024,
                system = systemPrompt,
                messages = mensajes,
                tools = tools
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            var response = await _httpClient.SendAsync(request);

            // Leemos el contenido para ver el error real provisto por Anthropic en caso de fallar
            var contenido = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Anthropic API error {response.StatusCode}: {contenido}");

            return JsonDocument.Parse(contenido);
        }
    }
}