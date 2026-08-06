using System.Text.Json;
using Microsoft.Extensions.Options;
using Omni.Application.Configuration;
using Omni.Application.Interfaces;

namespace Omni.Infrastructure.Providers;

/// <summary>
/// Drives the server side of Meta's Embedded Signup flows (WhatsApp Embedded Signup,
/// Facebook Login for Business for Messenger/Instagram): exchanges the authorization code
/// the frontend received from FB.login for a token, discovers which account was granted,
/// and subscribes this app to that account's webhook events.
///
/// NOTE: Meta revises Embedded Signup's exact request shape periodically. The endpoints used
/// here (oauth/access_token, debug_token, phone_numbers, me/accounts, subscribed_apps) have
/// been stable for years, but double-check the code-exchange step (redirectUri handling) and
/// the subscribed_fields values against the current Meta docs when wiring up the real App ID.
/// </summary>
public sealed class MetaEmbeddedSignupService : IMetaEmbeddedSignupService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MetaSettings _settings;

    public MetaEmbeddedSignupService(IHttpClientFactory httpClientFactory, IOptions<MetaSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    public async Task<MetaEmbeddedSignupResult> CompleteWhatsAppSignupAsync(string code, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("GraphApi");
        var userToken = await ExchangeCodeAsync(client, code, cancellationToken);
        var wabaId = await DiscoverWhatsAppBusinessAccountIdAsync(client, userToken, cancellationToken);

        var phoneNumbersJson = await GetAsync(client, $"{wabaId}/phone_numbers", userToken, cancellationToken);
        using var phoneDoc = JsonDocument.Parse(phoneNumbersJson);
        var firstPhone = phoneDoc.RootElement.GetProperty("data").EnumerateArray().FirstOrDefault();
        if (firstPhone.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException("No phone number is registered under the granted WhatsApp Business Account.");
        }

        var phoneNumberId = firstPhone.GetProperty("id").GetString()!;
        var displayNumber = firstPhone.TryGetProperty("display_phone_number", out var dn) ? dn.GetString() : phoneNumberId;

        await PostAsync(client, $"{wabaId}/subscribed_apps", userToken, cancellationToken);

        return new MetaEmbeddedSignupResult(phoneNumberId, userToken, displayNumber ?? phoneNumberId);
    }

    public async Task<MetaEmbeddedSignupResult> CompleteMessengerSignupAsync(string code, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("GraphApi");
        var userToken = await ExchangeCodeAsync(client, code, cancellationToken);
        var page = await DiscoverFirstPageAsync(client, userToken, cancellationToken);

        await PostAsync(client, $"{page.Id}/subscribed_apps?subscribed_fields=messages,messaging_postbacks", page.AccessToken, cancellationToken);

        return new MetaEmbeddedSignupResult(page.Id, page.AccessToken, page.Name);
    }

    public async Task<MetaEmbeddedSignupResult> CompleteInstagramSignupAsync(string code, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("GraphApi");
        var userToken = await ExchangeCodeAsync(client, code, cancellationToken);
        var page = await DiscoverFirstPageAsync(client, userToken, cancellationToken);

        var pageJson = await GetAsync(client, $"{page.Id}?fields=instagram_business_account", page.AccessToken, cancellationToken);
        using var pageDoc = JsonDocument.Parse(pageJson);
        if (!pageDoc.RootElement.TryGetProperty("instagram_business_account", out var igAccount))
        {
            throw new InvalidOperationException("The connected Facebook Page has no linked Instagram Business account.");
        }

        var igAccountId = igAccount.GetProperty("id").GetString()!;

        await PostAsync(client, $"{page.Id}/subscribed_apps?subscribed_fields=messages", page.AccessToken, cancellationToken);

        return new MetaEmbeddedSignupResult(igAccountId, page.AccessToken, page.Name);
    }

    private async Task<string> ExchangeCodeAsync(HttpClient client, string code, CancellationToken cancellationToken)
    {
        var url = $"https://graph.facebook.com/{_settings.GraphApiVersion}/oauth/access_token" +
                   $"?client_id={Uri.EscapeDataString(_settings.AppId)}" +
                   $"&client_secret={Uri.EscapeDataString(_settings.AppSecret)}" +
                   $"&code={Uri.EscapeDataString(code)}";

        var response = await client.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Meta code exchange failed ({(int)response.StatusCode}): {body}");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    private async Task<string> DiscoverWhatsAppBusinessAccountIdAsync(HttpClient client, string token, CancellationToken cancellationToken)
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
                    var firstId = targetIds.EnumerateArray().FirstOrDefault();
                    if (firstId.ValueKind != JsonValueKind.Undefined)
                    {
                        return firstId.GetString()!;
                    }
                }
            }
        }

        throw new InvalidOperationException("No WhatsApp Business Account was granted in this signup — check the Embedded Signup configuration's permissions.");
    }

    private sealed record PageInfo(string Id, string Name, string AccessToken);

    private async Task<PageInfo> DiscoverFirstPageAsync(HttpClient client, string userToken, CancellationToken cancellationToken)
    {
        var json = await GetAsync(client, "me/accounts", userToken, cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var first = doc.RootElement.GetProperty("data").EnumerateArray().FirstOrDefault();
        if (first.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException("No Facebook Page was granted in this signup.");
        }

        return new PageInfo(
            first.GetProperty("id").GetString()!,
            first.GetProperty("name").GetString()!,
            first.GetProperty("access_token").GetString()!);
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
