using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;

namespace Omni.Infrastructure.Providers;

public sealed class HttpInboundMessageForwarder : IInboundMessageForwarder
{
    public const string SignatureHeaderName = "X-Omnichannel-Signature";
    private const string WebhookUrlSettingName = "IntegrationWebhookUrl";
    private const string WebhookSecretSettingName = "IntegrationWebhookSecret";

    private readonly IOrganizationSettingRepository _organizationSettingRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpInboundMessageForwarder> _logger;

    public HttpInboundMessageForwarder(
        IOrganizationSettingRepository organizationSettingRepository,
        IHttpClientFactory httpClientFactory,
        ILogger<HttpInboundMessageForwarder> logger)
    {
        _organizationSettingRepository = organizationSettingRepository;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task ForwardAsync(Guid organizationId, Conversation conversation, Customer customer, Message message, string publicBaseUrl, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            type = "message",
            conversationId = conversation.Id,
            customerId = customer.Id,
            customerName = customer.FullName,
            customerPhone = customer.WhatsAppNumber,
            channel = conversation.Channel.ToString(),
            message = new
            {
                id = message.Id,
                body = message.Body,
                messageType = message.MessageType,
                attachmentUrl = ToIntegrationsAttachmentUrl(message.AttachmentUrl, publicBaseUrl),
                direction = message.Direction,
                sentAt = message.SentAt
            }
        });

        await SendAsync(organizationId, payload, cancellationToken);
    }

    public async Task ForwardStatusUpdateAsync(Guid organizationId, string externalMessageId, string status, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            type = "status_update",
            externalMessageId,
            status,
            updatedAt = DateTimeOffset.UtcNow
        });

        await SendAsync(organizationId, payload, cancellationToken);
    }

    /// <summary>
    /// Rewrites the agent-only "/api/attachments/{id}/download" path (requires an agent JWT) into an
    /// absolute URL under "/api/integrations/attachments/{id}/download", which accepts the same API key
    /// an external integration already authenticates with — otherwise a forwarded attachment link would
    /// be unreachable by whatever system just received it.
    /// </summary>
    private static string? ToIntegrationsAttachmentUrl(string? attachmentUrl, string publicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(attachmentUrl))
        {
            return attachmentUrl;
        }

        var rewritten = attachmentUrl.Replace("/api/attachments/", "/api/integrations/attachments/", StringComparison.OrdinalIgnoreCase);
        return $"{publicBaseUrl.TrimEnd('/')}{rewritten}";
    }

    private async Task SendAsync(Guid organizationId, string payload, CancellationToken cancellationToken)
    {
        var settings = await _organizationSettingRepository.GetAllAsync(organizationId, cancellationToken);
        var webhookUrl = settings.FirstOrDefault(s => string.Equals(s.SettingName, WebhookUrlSettingName, StringComparison.OrdinalIgnoreCase))?.SettingValue;
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return;
        }

        var webhookSecret = settings.FirstOrDefault(s => string.Equals(s.SettingName, WebhookSecretSettingName, StringComparison.OrdinalIgnoreCase))?.SettingValue;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrWhiteSpace(webhookSecret))
            {
                request.Headers.TryAddWithoutValidation(SignatureHeaderName, Sign(payload, webhookSecret));
            }

            var client = _httpClientFactory.CreateClient("GraphApi");
            var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Forward to {WebhookUrl} for org {OrganizationId} returned {StatusCode}.", webhookUrl, organizationId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Never let a broken external webhook affect our own inbound message processing.
            _logger.LogWarning(ex, "Failed to forward to {WebhookUrl} for org {OrganizationId}.", webhookUrl, organizationId);
        }
    }

    private static string Sign(string payload, string secret) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload)));
}
