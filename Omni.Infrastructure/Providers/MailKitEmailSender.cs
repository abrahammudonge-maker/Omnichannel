using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Omni.Application.Interfaces;

namespace Omni.Infrastructure.Providers;

public sealed class MailKitEmailSender : IEmailSender
{
    public async Task SendAsync(
        string smtpHost,
        int smtpPort,
        string mailboxAddress,
        string mailboxPassword,
        string fromDisplayName,
        string toAddress,
        string subject,
        string body,
        EmailAttachment? attachment,
        CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromDisplayName, mailboxAddress));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;

        var builder = new BodyBuilder { TextBody = body };
        if (attachment is not null)
        {
            using var memoryStream = new MemoryStream();
            await attachment.Content.CopyToAsync(memoryStream, cancellationToken);
            builder.Attachments.Add(attachment.FileName, memoryStream.ToArray(), ContentType.Parse(attachment.ContentType));
        }
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.Auto, cancellationToken);
        await client.AuthenticateAsync(mailboxAddress, mailboxPassword, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
