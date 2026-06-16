using System.Net;
using System.Net.Mail;
using Task4.Models;

namespace Task4.Services;

public class EmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _config;

    public EmailService(ILogger<EmailService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    public Task SendConfirmationAsync(User user, string baseUrl)
    {
        return Task.Run(async () =>
        {
            var link = $"{baseUrl.TrimEnd('/')}/Account/Confirm?token={Uri.EscapeDataString(user.ConfirmationToken)}";
            var host = _config["Smtp:Host"];
            var from = _config["Smtp:From"];

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            {
                _logger.LogError("SMTP is not configured. Confirmation email was not sent to {Email}.", user.Email);
                return;
            }

            try
            {
                using var message = new MailMessage(from, user.Email)
                {
                    Subject = "Confirm your email",
                    Body = $"Hello {user.Name},\n\nConfirm your email using this link:\n{link}",
                    IsBodyHtml = false
                };

                using var client = new SmtpClient(host, GetPort())
                {
                    EnableSsl = GetBool("Smtp:EnableSsl", true)
                };

                var username = _config["Smtp:User"];
                var password = _config["Smtp:Password"];

                if (!string.IsNullOrWhiteSpace(username))
                {
                    client.Credentials = new NetworkCredential(username, password);
                }

                await client.SendMailAsync(message);
                _logger.LogInformation("Confirmation email sent to {Email}.", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Confirmation email failed for {Email}.", user.Email);
            }
        });
    }

    private int GetPort()
    {
        return int.TryParse(_config["Smtp:Port"], out var port) ? port : 587;
    }

    private bool GetBool(string key, bool defaultValue)
    {
        return bool.TryParse(_config[key], out var value) ? value : defaultValue;
    }
}
