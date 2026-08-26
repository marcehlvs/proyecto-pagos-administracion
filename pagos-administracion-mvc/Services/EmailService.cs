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

        // Plantilla visual compartida por todos los mails del sistema (bienvenida, avisos de cuotas,
        // notificaciones de pago, y los de Identity -- confirmación de cuenta, restablecer contraseña).
        // Usa una tabla con estilos inline a propósito: es lo único que se renderiza de forma
        // consistente entre clientes de mail (Gmail, Outlook, etc.), que ignoran <style> y CSS externo.
        public static string EnvolverPlantilla(string tituloInterno, string contenidoHtml, string? botonUrl = null, string? botonTexto = null)
        {
            var boton = string.IsNullOrEmpty(botonUrl) ? "" : $@"
            <tr>
                <td align=""center"" style=""padding:8px 0 4px 0;"">
                    <a href=""{botonUrl}"" style=""background-color:#10B981; color:#ffffff; text-decoration:none; padding:12px 30px; border-radius:8px; font-weight:600; font-size:15px; display:inline-block; font-family:Arial, Helvetica, sans-serif;"">{botonTexto}</a>
                </td>
            </tr>";

            return $@"<!DOCTYPE html>
<html lang=""es"">
<body style=""margin:0; padding:0; background-color:#F7FAFC; font-family:Arial, Helvetica, sans-serif;"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#F7FAFC; padding:24px 0;"">
        <tr>
            <td align=""center"">
                <table role=""presentation"" width=""480"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#ffffff; border-radius:12px; overflow:hidden; box-shadow:0 2px 8px rgba(0,0,0,0.08); max-width:480px;"">
                    <tr>
                        <td style=""background-color:#002045; padding:22px 32px;"">
                            <span style=""color:#ffffff; font-size:18px; font-weight:700; font-family:Arial, Helvetica, sans-serif;"">Escuela José de San Martín</span>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding:32px 32px 8px 32px;"">
                            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
                                <tr>
                                    <td style=""color:#1A365D; font-size:18px; font-weight:700; padding-bottom:14px; font-family:Arial, Helvetica, sans-serif;"">{tituloInterno}</td>
                                </tr>
                                <tr>
                                    <td style=""color:#181c1e; font-size:15px; line-height:1.6; font-family:Arial, Helvetica, sans-serif;"">{contenidoHtml}</td>
                                </tr>
                                {boton}
                            </table>
                        </td>
                    </tr>
                    <tr>
                        <td style=""background-color:#F7FAFC; padding:18px 32px; border-top:1px solid #E2E8F0;"">
                            <p style=""margin:0; font-size:12px; color:#4A5568; font-family:Arial, Helvetica, sans-serif;"">Este es un mensaje automático, no respondas este email.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        // Devuelve (true, null) si se mandó bien, o (false, mensaje) si falló -- así el llamador
        // puede decidir si le importa mostrarle el error a un admin en pantalla, sin tener que ir
        // a buscarlo en el log de consola cada vez que algo no llega.
        public async Task<(bool Exito, string? Error)> EnviarAsync(string destinatario, string asunto, string cuerpoHtml)
        {
            // El remitente NO debe ser el login SMTP (Smtp:User) -- eso es solo la credencial de
            // autenticación contra Brevo, casi nunca una dirección real. Brevo exige que el "From"
            // sea un email/dominio que vos verificaste como Sender en tu cuenta; si no coincide,
            // muchas veces acepta el SMTP (250 OK, sin excepción acá) y descarta el mail después,
            // sin avisar. Config: Smtp:FromEmail (si no está, cae a DatosEscuela:Email).
            var remitente = _config["Smtp:FromEmail"] ?? _config["DatosEscuela:Email"] ?? _config["Smtp:User"];

            var mensaje = new MimeMessage();
            mensaje.From.Add(new MailboxAddress("Escuela José de San Martín", remitente));
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
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando email a {Destinatario}", destinatario);
                return (false, ex.Message);
            }
        }
    }
}