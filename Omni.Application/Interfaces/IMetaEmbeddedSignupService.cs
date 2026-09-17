namespace Omni.Application.Interfaces;

public sealed record MetaEmbeddedSignupResult(
    string ExternalAccountId,
    string AccessToken,
    string DisplayName,
    string ExternalWabaId);

public sealed record MetaSignupCandidate(string Id, string DisplayName);

public sealed record MetaSignupDiscoveryResult(string SessionId, IReadOnlyList<MetaSignupCandidate> Candidates);

public interface IMetaEmbeddedSignupService
{
    /// <summary>
    /// Exchanges the code for a token and lists every WhatsApp phone number the login granted
    /// access to (across every WhatsApp Business Account granted), without finalizing anything yet.
    /// </summary>
    /// <param name="redirectUri">Must exactly match the redirect_uri the frontend passed to FB.login(), and must be registered under "Valid OAuth Redirect URIs" in the App Dashboard.</param>
    Task<MetaSignupDiscoveryResult> DiscoverWhatsAppCandidatesAsync(string code, string redirectUri, CancellationToken cancellationToken);

    /// <summary>Exchanges the code for a token and lists every Facebook Page the login granted access to.</summary>
    Task<MetaSignupDiscoveryResult> DiscoverMessengerCandidatesAsync(string code, string redirectUri, CancellationToken cancellationToken);

    /// <summary>Exchanges the code for a token and lists every Instagram Business Account linked to a granted Page.</summary>
    Task<MetaSignupDiscoveryResult> DiscoverInstagramCandidatesAsync(string code, string redirectUri, CancellationToken cancellationToken);

    /// <summary>
    /// Finalizes a signup for one specific candidate returned by a Discover call: subscribes
    /// this app to that account's webhook events and returns what's needed to save a channel account.
    /// The session is single-use and expires a few minutes after discovery.
    /// </summary>
    Task<MetaEmbeddedSignupResult> ConfirmSignupAsync(string sessionId, string selectedId, CancellationToken cancellationToken);
}
