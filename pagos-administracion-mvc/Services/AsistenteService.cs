using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace pagos_administracion_mvc.Services
{
    public class AsistenteService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string Modelo = "gemini-1.5-flash";

        public AsistenteService(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
            _apiKey = config["Gemini:ApiKey"]
                ?? throw new InvalidOperationException("Falta configurar Gemini:ApiKey en los user-secrets o appsettings.");
        }

        public async Task<JsonDocument> EnviarMensajeAsync(
            List<object> mensajes,
            object[] tools,
            string systemPrompt)
        {
            // Estructura exacta que requiere Google AI Studio / Gemini API
            var body = new
            {
                systemInstruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = mensajes,
                tools = tools
            };

            // Gemini recibe la API Key en la URL, no por Header
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Modelo}:generateContent?key={_apiKey}";

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(request);
            var contenido = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Gemini API error {response.StatusCode}: {contenido}");

            return JsonDocument.Parse(contenido);
        }
    }
}