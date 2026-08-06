using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Omni.Application.Interfaces;

namespace Omni.Infrastructure.Providers;

public sealed class GraphApiMessageSender : IMetaMessageSender
{
    private const string GraphApiVersion = "v21.0";
    private readonly IHttpClientFactory _httpClientFactory;

    public GraphApiMessageSender(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task SendAsync(
        string channelType,
        string accessToken,
        string platformId,
        string recipientId,
        string body,
        CancellationToken cancellationToken)
    {
        var payload = channelType switch
        {
            "WhatsApp" => JsonSerializer.Serialize(new
            {
                messaging_product = "whatsapp",
                to = recipientId,
                type = "text",
                text = new { body }
            }),
            "FacebookMessenger" or "Instagram" => JsonSerializer.Serialize(new
            {
                recipient = new { id = recipientId },
                message = new { text = body },
                messaging_type = "RESPONSE"
            }),
            _ => throw new InvalidOperationException($"Unsupported Meta channel type: {channelType}")
        };

        var client = _httpClientFactory.CreateClient("GraphApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/{GraphApiVersion}/{platformId}/messages")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Graph API send failed ({(int)response.StatusCode}): {responseBody}");
        }
    }
}
