namespace Omni.Application.Interfaces;

public sealed record MetaEmbeddedSignupResult(
    string ExternalAccountId,
    string AccessToken,
    string DisplayName);

public interface IMetaEmbeddedSignupService
{
    /// <summary>
    /// Completes the WhatsApp Embedded Signup flow: exchanges the code for a token,
    /// discovers the WhatsApp Business Account + phone number granted, and subscribes
    /// this app to that WABA's webhook events.
    /// </summary>
    Task<MetaEmbeddedSignupResult> CompleteWhatsAppSignupAsync(string code, CancellationToken cancellationToken);

    /// <summary>
    /// Completes the Facebook Login for Business flow for a Page: exchanges the code for a
    /// user token, discovers the Page + its Page Access Token, and subscribes the Page to
    /// this app's webhook events.
    /// </summary>
    Task<MetaEmbeddedSignupResult> CompleteMessengerSignupAsync(string code, CancellationToken cancellationToken);

    /// <summary>
    /// Completes the Facebook Login for Business flow for Instagram: exchanges the code,
    /// discovers the Page's linked Instagram Business Account, and subscribes it.
    /// </summary>
    Task<MetaEmbeddedSignupResult> CompleteInstagramSignupAsync(string code, CancellationToken cancellationToken);
}
