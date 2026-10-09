namespace Omni.Application.Configuration;

public sealed class MetaSettings
{
    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string WebhookVerifyToken { get; set; } = string.Empty;
    // Keep this in step with the Meta Embedded Signup exchange endpoint.
    // A retired Graph version causes every outbound request to fail before WhatsApp can process it.
    public string GraphApiVersion { get; set; } = "v25.0";
}
