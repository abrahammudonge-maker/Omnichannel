namespace Omni.Application.Configuration;

public sealed class MetaSettings
{
    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string WebhookVerifyToken { get; set; } = string.Empty;
    public string GraphApiVersion { get; set; } = "v21.0";
}
