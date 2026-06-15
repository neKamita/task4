using Task4.Models;

namespace Task4.Services;

public class EmailService
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
    }

    public Task SendConfirmationAsync(User user, string baseUrl)
    {
        return Task.Run(() =>
        {
            var link = $"{baseUrl.TrimEnd('/')}/Account/Confirm?token={Uri.EscapeDataString(user.ConfirmationToken)}";
            _logger.LogInformation("Confirmation link for {Email}: {Link}", user.Email, link);
        });
    }
}
