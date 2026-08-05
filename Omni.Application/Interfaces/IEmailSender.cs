namespace Omni.Application.Interfaces;

public interface IEmailSender
{
    Task SendAsync(
        string smtpHost,
        int smtpPort,
        string mailboxAddress,
        string mailboxPassword,
        string fromDisplayName,
        string toAddress,
        string subject,
        string body,
        CancellationToken cancellationToken);
}
