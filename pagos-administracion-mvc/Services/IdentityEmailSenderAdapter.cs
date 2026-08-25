using Microsoft.AspNetCore.Identity.UI.Services;

namespace pagos_administracion_mvc.Services
{
    public class IdentityEmailSenderAdapter : IEmailSender
    {
        private readonly EmailService _emailService;

        public IdentityEmailSenderAdapter(EmailService emailService)
        {
            _emailService = emailService;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
            => await _emailService.EnviarAsync(email, subject, htmlMessage);
    }
}