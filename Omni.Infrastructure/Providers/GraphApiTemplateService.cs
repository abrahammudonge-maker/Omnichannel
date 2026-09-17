using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Omni.Application.Configuration;
using Omni.Application.Interfaces;

namespace Omni.Infrastructure.Providers;

public sealed class GraphApiTemplateService : IMetaTemplateService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MetaSettings _settings;

    public GraphApiTemplateService(IHttpClientFactory httpClientFactory, IOptions<MetaSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    public async Task<IReadOnlyList<MetaTemplateInfo>> FetchTemplatesAsync(string wabaId, string accessToken, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("GraphApi");
        var results = new List<MetaTemplateInfo>();
        var url = $"https://graph.facebook.com/{_settings.GraphApiVersion}/{wabaId}/message_templates?fields=name,language,category,status,components&limit=100";

        while (!string.IsNullOrEmpty(url))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Fetching WhatsApp templates for {wabaId} failed ({(int)response.StatusCode}): {body}");
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.TryGetProperty("data", out var data))
            {
                foreach (var item in data.EnumerateArray())
                {
                    var name = item.GetProperty("name").GetString() ?? string.Empty;
                    var language = item.TryGetProperty("language", out var languageProp) ? languageProp.GetString() ?? string.Empty : string.Empty;
                    var category = item.TryGetProperty("category", out var categoryProp) ? categoryProp.GetString() ?? string.Empty : string.Empty;
                    var status = item.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? string.Empty : string.Empty;

                    string? literalBodyText = null;
                    bool? addSecurityRecommendation = null;
                    int? codeExpirationMinutes = null;
                    var componentsJson = "[]";
                    if (item.TryGetProperty("components", out var components))
                    {
                        componentsJson = components.GetRawText();
                        foreach (var component in components.EnumerateArray())
                        {
                            if (!component.TryGetProperty("type", out var typeProp))
                            {
                                continue;
                            }
                            var componentType = typeProp.GetString();

                            if (string.Equals(componentType, "BODY", StringComparison.OrdinalIgnoreCase))
                            {
                                if (component.TryGetProperty("text", out var textProp))
                                {
                                    literalBodyText = textProp.GetString();
                                }
                                if (component.TryGetProperty("add_security_recommendation", out var addRecProp))
                                {
                                    addSecurityRecommendation = addRecProp.GetBoolean();
                                }
                            }
                            else if (string.Equals(componentType, "FOOTER", StringComparison.OrdinalIgnoreCase) &&
                                     component.TryGetProperty("code_expiration_minutes", out var expProp))
                            {
                                codeExpirationMinutes = expProp.GetInt32();
                            }
                        }
                    }

                    // AUTHENTICATION templates carry no literal body text — Meta writes the wording itself —
                    // so reconstruct a readable equivalent from the flags for display/preview purposes.
                    var bodyText = literalBodyText
                        ?? (string.Equals(category, "AUTHENTICATION", StringComparison.OrdinalIgnoreCase)
                            ? SynthesizeAuthenticationBodyText(addSecurityRecommendation ?? true, codeExpirationMinutes)
                            : string.Empty);

                    results.Add(new MetaTemplateInfo(name, language, category, status, bodyText, componentsJson));
                }
            }

            url = root.TryGetProperty("paging", out var paging) && paging.TryGetProperty("next", out var nextProp)
                ? nextProp.GetString()
                : null;
        }

        return results;
    }

    private static readonly Regex VariablePattern = new(@"\{\{(\d+)\}\}", RegexOptions.Compiled);

    public async Task<string> CreateTemplateAsync(
        string wabaId,
        string accessToken,
        string name,
        string language,
        string category,
        string? headerText,
        string bodyText,
        string? footerText,
        IReadOnlyList<string>? quickReplyButtons,
        CancellationToken cancellationToken)
    {
        var components = new List<object>();

        if (!string.IsNullOrWhiteSpace(headerText))
        {
            components.Add(new { type = "HEADER", format = "TEXT", text = headerText });
        }

        var variableCount = VariablePattern.Matches(bodyText).Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();
        if (variableCount > 0)
        {
            var exampleValues = Enumerable.Range(1, variableCount).Select(i => $"Example{i}").ToArray();
            components.Add(new { type = "BODY", text = bodyText, example = new { body_text = new[] { exampleValues } } });
        }
        else
        {
            components.Add(new { type = "BODY", text = bodyText });
        }

        if (!string.IsNullOrWhiteSpace(footerText))
        {
            components.Add(new { type = "FOOTER", text = footerText });
        }

        if (quickReplyButtons is { Count: > 0 })
        {
            components.Add(new
            {
                type = "BUTTONS",
                buttons = quickReplyButtons.Select(text => new { type = "QUICK_REPLY", text }).ToArray()
            });
        }

        return await SubmitTemplateAsync(wabaId, accessToken, name, language, category, components, cancellationToken);
    }

    public async Task<string> CreateAuthenticationTemplateAsync(
        string wabaId,
        string accessToken,
        string name,
        string language,
        bool addSecurityRecommendation,
        int? codeExpirationMinutes,
        CancellationToken cancellationToken)
    {
        var components = new List<object>
        {
            new { type = "BODY", add_security_recommendation = addSecurityRecommendation }
        };

        if (codeExpirationMinutes is int minutes)
        {
            components.Add(new { type = "FOOTER", code_expiration_minutes = minutes });
        }

        // Mandatory — Meta rejects creating an AUTHENTICATION template without exactly one OTP button
        // (verified empirically: error 2388148, "must have exactly one button... OTP type").
        // COPY_CODE works for any customer on any device without extra setup, unlike ONE_TAP/ZERO_TAP
        // which require registering the client app's package name and signature hash with Meta.
        components.Add(new
        {
            type = "BUTTONS",
            buttons = new[] { new { type = "OTP", otp_type = "COPY_CODE" } }
        });

        return await SubmitTemplateAsync(wabaId, accessToken, name, language, "AUTHENTICATION", components, cancellationToken);
    }

    private static string SynthesizeAuthenticationBodyText(bool addSecurityRecommendation, int? codeExpirationMinutes)
    {
        var text = "{{1}} is your verification code.";
        if (addSecurityRecommendation)
        {
            text += " For your security, do not share this code.";
        }
        if (codeExpirationMinutes is int minutes)
        {
            text += $" This code expires in {minutes} minutes.";
        }
        return text;
    }

    private async Task<string> SubmitTemplateAsync(
        string wabaId, string accessToken, string name, string language, string category, List<object> components, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { name, language, category, components });

        var client = _httpClientFactory.CreateClient("GraphApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/{_settings.GraphApiVersion}/{wabaId}/message_templates")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Creating WhatsApp template '{name}' failed ({(int)response.StatusCode}): {body}");
        }

        return JsonSerializer.Serialize(components);
    }
}
