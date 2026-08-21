using MailKit.Net.Smtp;
using MimeKit;

namespace pagos_administracion_mvc.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml)
        {
            var mensaje = new MimeMessage();
            mensaje.From.Add(new MailboxAddress("Escuela José de San Martín", _config["Smtp:User"]));
            mensaje.To.Add(MailboxAddress.Parse(destinatario));
            mensaje.Subject = asunto;
            mensaje.Body = new TextPart("html") { Text = cuerpoHtml };

            try
            {
                using var cliente = new SmtpClient();
                await cliente.ConnectAsync(_config["Smtp:Host"], int.Parse(_config["Smtp:Port"]!), MailKit.Security.SecureSocketOptions.StartTls);
                await cliente.AuthenticateAsync(_config["Smtp:User"], _config["Smtp:Password"]);
                await cliente.SendAsync(mensaje);
                await cliente.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando email a {Destinatario}", destinatario);
            }
        }
    }
}