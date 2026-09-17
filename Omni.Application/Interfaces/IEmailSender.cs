namespace Omni.Application.Interfaces;

public sealed record EmailAttachment(string FileName, string ContentType, Stream Content);

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
        EmailAttachment? attachment,
        CancellationToken cancellationToken);
}
