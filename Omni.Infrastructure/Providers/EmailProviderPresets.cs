namespace Omni.Infrastructure.Providers;

public readonly record struct EmailProviderPreset(string SmtpHost, int SmtpPort, string ImapHost, int ImapPort);

public static class EmailProviderPresets
{
    private static readonly Dictionary<string, EmailProviderPreset> ByDomain = new(StringComparer.OrdinalIgnoreCase)
    {
        ["outlook.com"] = new("smtp-mail.outlook.com", 587, "outlook.office365.com", 993),
        ["hotmail.com"] = new("smtp-mail.outlook.com", 587, "outlook.office365.com", 993),
        ["live.com"] = new("smtp-mail.outlook.com", 587, "outlook.office365.com", 993),
        ["msn.com"] = new("smtp-mail.outlook.com", 587, "outlook.office365.com", 993),
        ["gmail.com"] = new("smtp.gmail.com", 587, "imap.gmail.com", 993),
        ["yahoo.com"] = new("smtp.mail.yahoo.com", 587, "imap.mail.yahoo.com", 993),
        ["zoho.com"] = new("smtp.zoho.com", 587, "imap.zoho.com", 993),
        ["icloud.com"] = new("smtp.mail.me.com", 587, "imap.mail.me.com", 993),
        ["me.com"] = new("smtp.mail.me.com", 587, "imap.mail.me.com", 993),
        ["mac.com"] = new("smtp.mail.me.com", 587, "imap.mail.me.com", 993)
    };

    public static EmailProviderPreset? Resolve(string emailAddress)
    {
        var domain = emailAddress.Split('@').LastOrDefault();
        return domain is not null && ByDomain.TryGetValue(domain, out var preset) ? preset : null;
    }
}
