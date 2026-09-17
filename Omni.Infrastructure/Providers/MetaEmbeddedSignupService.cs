using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Omni.Application.Configuration;
using Omni.Application.Interfaces;

namespace Omni.Infrastructure.Providers;

/// <summary>
/// Drives the server side of Meta's Embedded Signup flows (WhatsApp Embedded Signup,
/// Facebook Login for Business for Messenger/Instagram) as a two-step discover/confirm
/// process: Discover exchanges the code and lists every candidate account the login granted
/// access to (a business can have more than one WABA/Page); Confirm finalizes whichever one
/// the user picks. Candidates are held server-side under a short-lived session id between
/// the two calls — the frontend never sees the raw Meta access token.
///
/// NOTE: Meta revises Embedded Signup's exact request shape periodically. The endpoints used
/// here (oauth/access_token, debug_token, phone_numbers, me/accounts, subscribed_apps) have
/// been stable for years, but double-check the code-exchange step (redirectUri handling) and
/// the subscribed_fields values against the current Meta docs when wiring up the real App ID.
/// </summary>
public sealed class MetaEmbeddedSignupService : IMetaEmbeddedSignupService
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(10);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly MetaSettings _settings;

    public MetaEmbeddedSignupService(IHttpClientFactory httpClientFactory, IMemoryCache cache, IOptions<MetaSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _settings = settings.Value;
    }

    private sealed record PendingCandidate(string DisplayName, string AccessToken, string SubscribeTargetId, string SubscribeQuery);
    private sealed record PendingSession(Dictionary<string, PendingCandidate> Candidates);

    public async Task<MetaSignupDiscoveryResult> DiscoverWhatsAppCandidatesAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("GraphApi");
        var userToken = await ExchangeCodeAsync(client, code, redirectUri, cancellationToken);
        var wabaIds = await DiscoverWhatsAppBusinessAccountIdsAsync(client, userToken, cancellationToken);

        var candidates = new Dictionary<string, PendingCandidate>();
        var displayList = new List<MetaSignupCandidate>();
        foreach (var wabaId in wabaIds)
        {
            string wabaName;
            try
            {
                var wabaJson = await GetAsync(client, $"{wabaId}?fields=name", userToken, cancellationToken);
                using var wabaDoc = JsonDocument.Parse(wabaJson);
                wabaName = wabaDoc.RootElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? wabaId : wabaId;
            }
            catch (InvalidOperationException)
            {
                wabaName = wabaId;
            }

            var phoneNumbersJson = await GetAsync(client, $"{wabaId}/phone_numbers", userToken, cancellationToken);
            using var phoneDoc = JsonDocument.Parse(phoneNumbersJson);
            foreach (var phone in phoneDoc.RootElement.GetProperty("data").EnumerateArray())
            {
                var phoneNumberId = phone.GetProperty("id").GetString()!;
                var displayNumber = phone.TryGetProperty("display_phone_number", out var dn) ? dn.GetString() : phoneNumberId;
                var label = $"{displayNumber ?? phoneNumberId} — {wabaName}";
                candidates[phoneNumberId] = new PendingCandidate(displayNumber ?? phoneNumberId, userToken, wabaId, string.Empty);
                displayList.Add(new MetaSignupCandidate(phoneNumberId, label));
            }
        }

        if (displayList.Count == 0)
        {
            throw new InvalidOperationException("No phone number is registered under any WhatsApp Business Account granted in this signup.");
        }

        return new MetaSignupDiscoveryResult(StoreSession(candidates), displayList);
    }

    public async Task<MetaSignupDiscoveryResult> DiscoverMessengerCandidatesAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("GraphApi");
        var userToken = await ExchangeCodeAsync(client, code, redirectUri, cancellationToken);
        var pages = await DiscoverPagesAsync(client, userToken, cancellationToken);

        if (pages.Count == 0)
        {
            throw new InvalidOperationException("No Facebook Page was granted in this signup.");
        }

        var candidates = new Dictionary<string, PendingCandidate>();
        var displayList = new List<MetaSignupCandidate>();
        foreach (var page in pages)
        {
            candidates[page.Id] = new PendingCandidate(page.Name, page.AccessToken, page.Id, "subscribed_fields=messages,messaging_postbacks,message_deliveries,message_reads");
            displayList.Add(new MetaSignupCandidate(page.Id, page.Name));
        }

        return new MetaSignupDiscoveryResult(StoreSession(candidates), displayList);
    }

    public async Task<MetaSignupDiscoveryResult> DiscoverInstagramCandidatesAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("GraphApi");
        var userToken = await ExchangeCodeAsync(client, code, redirectUri, cancellationToken);
        var pages = await DiscoverPagesAsync(client, userToken, cancellationToken);

        var candidates = new Dictionary<string, PendingCandidate>();
        var displayList = new List<MetaSignupCandidate>();
        foreach (var page in pages)
        {
            var pageJson = await GetAsync(client, $"{page.Id}?fields=instagram_business_account", page.AccessToken, cancellationToken);
            using var pageDoc = JsonDocument.Parse(pageJson);
            if (!pageDoc.RootElement.TryGetProperty("instagram_business_account", out var igAccount))
            {
                continue;
            }

            var igAccountId = igAccount.GetProperty("id").GetString()!;
            candidates[igAccountId] = new PendingCandidate(page.Name, page.AccessToken, page.Id, "subscribed_fields=messages");
            displayList.Add(new MetaSignupCandidate(igAccountId, page.Name));
        }

        if (displayList.Count == 0)
        {
            throw new InvalidOperationException("None of the Facebook Pages granted in this signup have a linked Instagram Business account.");
        }

        return new MetaSignupDiscoveryResult(StoreSession(candidates), displayList);
    }

    public Task<MetaEmbeddedSignupResult> ConfirmSignupAsync(string sessionId, string selectedId, CancellationToken cancellationToken)
    {
        if (!_cache.TryGetValue(CacheKey(sessionId), out PendingSession? session) || session is null)
        {
            throw new InvalidOperationException("This signup has expired — please connect again.");
        }

        if (!session.Candidates.TryGetValue(selectedId, out var candidate))
        {
            throw new InvalidOperationException("That option is no longer available — please connect again.");
        }

        _cache.Remove(CacheKey(sessionId));
        return ConfirmCandidateAsync(candidate, selectedId, cancellationToken);
    }

    private async Task<MetaEmbeddedSignupResult> ConfirmCandidateAsync(PendingCandidate candidate, string selectedId, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("GraphApi");
        var subscribePath = string.IsNullOrEmpty(candidate.SubscribeQuery)
            ? $"{candidate.SubscribeTargetId}/subscribed_apps"
            : $"{candidate.SubscribeTargetId}/subscribed_apps?{candidate.SubscribeQuery}";
        await PostAsync(client, subscribePath, candidate.AccessToken, cancellationToken);

        return new MetaEmbeddedSignupResult(selectedId, candidate.AccessToken, candidate.DisplayName, candidate.SubscribeTargetId);
    }

    private string StoreSession(Dictionary<string, PendingCandidate> candidates)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        _cache.Set(CacheKey(sessionId), new PendingSession(candidates), SessionLifetime);
        return sessionId;
    }

    private static string CacheKey(string sessionId) => $"meta-signup:{sessionId}";

    private async Task<string> ExchangeCodeAsync(HttpClient client, string code, string redirectUri, CancellationToken cancellationToken)
    {
        // Deliberately not using _settings.GraphApiVersion (v21.0) here — the App Dashboard's own
        // sample requests for this app use v25.0, and this endpoint's behavior for Business Login
        // code exchange may differ from the older version even though other Graph calls still work on it.
        var url = "https://graph.facebook.com/v25.0/oauth/access_token" +
                   $"?client_id={Uri.EscapeDataString(_settings.AppId)}" +
                   $"&client_secret={Uri.EscapeDataString(_settings.AppSecret)}" +
                   $"&code={Uri.EscapeDataString(code)}" +
                   $"&redirect_uri={Uri.EscapeDataString(redirectUri)}";

        var response = await client.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Meta code exchange failed ({(int)response.StatusCode}): {body}");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    private async Task<IReadOnlyList<string>> DiscoverWhatsAppBusinessAccountIdsAsync(HttpClient client, string token, CancellationToken cancellationToken)
    {
        var appToken = $"{_settings.AppId}|{_settings.AppSecret}";
        var json = await GetAsync(client, $"debug_token?input_token={Uri.EscapeDataString(token)}&access_token={Uri.EscapeDataString(appToken)}", null, cancellationToken);

        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        if (data.TryGetProperty("granular_scopes", out var scopes))
        {
            foreach (var scope in scopes.EnumerateArray())
            {
                if (scope.GetProperty("scope").GetString() == "whatsapp_business_management" &&
                    scope.TryGetProperty("target_ids", out var targetIds))
                {
                    var ids = targetIds.EnumerateArray().Select(id => id.GetString()!).ToList();
                    if (ids.Count > 0)
                    {
                        return ids;
                    }
                }
            }
        }

        throw new InvalidOperationException("No WhatsApp Business Account was granted in this signup — check the Embedded Signup configuration's permissions.");
    }

    private sealed record PageInfo(string Id, string Name, string AccessToken);

    private async Task<IReadOnlyList<PageInfo>> DiscoverPagesAsync(HttpClient client, string userToken, CancellationToken cancellationToken)
    {
        var json = await GetAsync(client, "me/accounts", userToken, cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("data").EnumerateArray()
            .Select(p => new PageInfo(p.GetProperty("id").GetString()!, p.GetProperty("name").GetString()!, p.GetProperty("access_token").GetString()!))
            .ToList();
    }

    private async Task<string> GetAsync(HttpClient client, string path, string? token, CancellationToken cancellationToken)
    {
        var separator = path.Contains('?') ? "&" : "?";
        var url = $"https://graph.facebook.com/{_settings.GraphApiVersion}/{path}" +
                   (token is null ? string.Empty : $"{separator}access_token={Uri.EscapeDataString(token)}");

        var response = await client.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Graph API call to {path} failed ({(int)response.StatusCode}): {body}");
        }

        return body;
    }

    private async Task PostAsync(HttpClient client, string path, string token, CancellationToken cancellationToken)
    {
        var separator = path.Contains('?') ? "&" : "?";
        var url = $"https://graph.facebook.com/{_settings.GraphApiVersion}/{path}{separator}access_token={Uri.EscapeDataString(token)}";

        var response = await client.PostAsync(url, null, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Graph API subscription call to {path} failed ({(int)response.StatusCode}): {body}");
        }
    }
}
