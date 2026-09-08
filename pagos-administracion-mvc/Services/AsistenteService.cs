using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace pagos_administracion_mvc.Services
{
    public class AsistenteService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string Modelo = "openai/gpt-oss-20b";

        public AsistenteService(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
            _apiKey = config["Groq:ApiKey"]
                ?? throw new InvalidOperationException("Falta configurar Groq:ApiKey en los user-secrets.");
        }

        public async Task<JsonDocument> EnviarMensajeAsync(List<object> mensajes, object[] tools)
        {
            var body = new
            {
                model = Modelo,
                messages = mensajes,
                tools = tools,
                tool_choice = "auto"
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.SendAsync(request);
            var contenido = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Groq API error {response.StatusCode}: {contenido}");

            return JsonDocument.Parse(contenido);
        }
    }
}