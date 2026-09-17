using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Omni.Application.Configuration;
using Omni.Application.Interfaces;

namespace Omni.Infrastructure.Providers;

public sealed class GraphApiMessageSender : IMetaMessageSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MetaSettings _settings;

    public GraphApiMessageSender(IHttpClientFactory httpClientFactory, IOptions<MetaSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    public async Task<MetaMessageSendResult> SendAsync(
        string channelType,
        string accessToken,
        string platformId,
        string recipientId,
        string body,
        MetaOutboundAttachment? attachment,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("GraphApi");

        if (attachment is null)
        {
            var textMessageId = await SendTextAsync(client, channelType, accessToken, platformId, recipientId, body, cancellationToken);
            return new MetaMessageSendResult(textMessageId);
        }

        return channelType switch
        {
            "WhatsApp" => new MetaMessageSendResult(await SendWhatsAppMediaAsync(client, accessToken, platformId, recipientId, body, attachment, cancellationToken)),
            "FacebookMessenger" or "Instagram" => new MetaMessageSendResult(await SendMessengerMediaAsync(client, accessToken, platformId, recipientId, body, attachment, cancellationToken)),
            _ => throw new InvalidOperationException($"Unsupported Meta channel type: {channelType}")
        };
    }

    public async Task<MetaMessageSendResult> SendTemplateAsync(
        string accessToken,
        string phoneNumberId,
        string recipientId,
        string templateName,
        string language,
        string category,
        IReadOnlyList<string> bodyParameters,
        CancellationToken cancellationToken)
    {
        var template = new Dictionary<string, object>
        {
            ["name"] = templateName,
            ["language"] = new { code = language }
        };

        if (string.Equals(category, "AUTHENTICATION", StringComparison.OrdinalIgnoreCase))
        {
            if (bodyParameters.Count != 1)
            {
                throw new InvalidOperationException("An AUTHENTICATION template requires exactly one body parameter — the verification code.");
            }

            // Confirmed empirically (not just docs): omitting the button entirely gets rejected with
            // "Button at index 0 of type Url requires a parameter" — Meta genuinely treats this button
            // as type Url at send time (despite the template being created as an OTP/COPY_CODE button),
            // and it must be included. sub_type "copy_code" is rejected outright (error 132018, "must be
            // of type Url"). This "url" shape is the one Meta's synchronous validation actually accepts.
            var code = bodyParameters[0];
            template["components"] = new object[]
            {
                new { type = "body", parameters = new[] { new { type = "text", text = code } } },
                new
                {
                    type = "button",
                    sub_type = "url",
                    index = "0",
                    parameters = new[] { new { type = "text", text = code } }
                }
            };
        }
        else if (bodyParameters.Count > 0)
        {
            template["components"] = new object[]
            {
                new
                {
                    type = "body",
                    parameters = bodyParameters.Select(value => new { type = "text", text = value }).ToArray()
                }
            };
        }

        var payload = JsonSerializer.Serialize(new
        {
            messaging_product = "whatsapp",
            to = recipientId,
            type = "template",
            template
        });

        var client = _httpClientFactory.CreateClient("GraphApi");
        var messageId = await PostMessageAsync(client, accessToken, phoneNumberId, payload, cancellationToken);
        return new MetaMessageSendResult(messageId);
    }

    private async Task<string> SendTextAsync(HttpClient client, string channelType, string accessToken, string platformId, string recipientId, string body, CancellationToken cancellationToken)
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

        return await PostMessageAsync(client, accessToken, platformId, payload, cancellationToken);
    }

    private async Task<string> SendWhatsAppMediaAsync(HttpClient client, string accessToken, string platformId, string recipientId, string body, MetaOutboundAttachment attachment, CancellationToken cancellationToken)
    {
        var mediaType = WhatsAppMediaType(attachment.ContentType);
        var mediaId = await UploadWhatsAppMediaAsync(client, accessToken, platformId, attachment, cancellationToken);

        var mediaObject = new Dictionary<string, object> { ["id"] = mediaId };
        if (mediaType == "document")
        {
            mediaObject["filename"] = attachment.FileName;
        }
        if (mediaType != "audio" && !string.IsNullOrWhiteSpace(body))
        {
            mediaObject["caption"] = body;
        }

        var payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["messaging_product"] = "whatsapp",
            ["to"] = recipientId,
            ["type"] = mediaType,
            [mediaType] = mediaObject
        });

        return await PostMessageAsync(client, accessToken, platformId, payload, cancellationToken);
    }

    private async Task<string> UploadWhatsAppMediaAsync(HttpClient client, string accessToken, string platformId, MetaOutboundAttachment attachment, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("whatsapp"), "messaging_product");
        var fileContent = new StreamContent(attachment.Content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(attachment.ContentType);
        content.Add(fileContent, "file", attachment.FileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/{_settings.GraphApiVersion}/{platformId}/media")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"WhatsApp media upload failed ({(int)response.StatusCode}): {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        var mediaId = document.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(mediaId))
        {
            throw new InvalidOperationException("WhatsApp media upload succeeded but did not return a media ID.");
        }
        return mediaId;
    }

    private async Task<string> SendMessengerMediaAsync(HttpClient client, string accessToken, string platformId, string recipientId, string body, MetaOutboundAttachment attachment, CancellationToken cancellationToken)
    {
        var attachmentType = MessengerAttachmentType(attachment.ContentType);
        var attachmentId = await UploadMessengerAttachmentAsync(client, accessToken, platformId, attachmentType, attachment, cancellationToken);

        var attachmentPayload = JsonSerializer.Serialize(new
        {
            recipient = new { id = recipientId },
            message = new { attachment = new { type = attachmentType, payload = new { attachment_id = attachmentId } } },
            messaging_type = "RESPONSE"
        });
        var attachmentMessageId = await PostMessageAsync(client, accessToken, platformId, attachmentPayload, cancellationToken);

        if (string.IsNullOrWhiteSpace(body))
        {
            return attachmentMessageId;
        }

        var textPayload = JsonSerializer.Serialize(new
        {
            recipient = new { id = recipientId },
            message = new { text = body },
            messaging_type = "RESPONSE"
        });
        return await PostMessageAsync(client, accessToken, platformId, textPayload, cancellationToken);
    }

    private async Task<string> UploadMessengerAttachmentAsync(HttpClient client, string accessToken, string platformId, string attachmentType, MetaOutboundAttachment attachment, CancellationToken cancellationToken)
    {
        var messageJson = JsonSerializer.Serialize(new { attachment = new { type = attachmentType, payload = new { is_reusable = true } } });

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(messageJson), "message");
        var fileContent = new StreamContent(attachment.Content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(attachment.ContentType);
        content.Add(fileContent, "filedata", attachment.FileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/{_settings.GraphApiVersion}/{platformId}/message_attachments")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Messenger attachment upload failed ({(int)response.StatusCode}): {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        var attachmentId = document.RootElement.TryGetProperty("attachment_id", out var idProp) ? idProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(attachmentId))
        {
            throw new InvalidOperationException("Messenger attachment upload succeeded but did not return an attachment ID.");
        }
        return attachmentId;
    }

    private async Task<string> PostMessageAsync(HttpClient client, string accessToken, string platformId, string jsonPayload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/{_settings.GraphApiVersion}/{platformId}/messages")
        {
            Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Graph API send failed ({(int)response.StatusCode}): {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        var messageId = root.TryGetProperty("message_id", out var messageIdProperty)
            ? messageIdProperty.GetString()
            : root.TryGetProperty("messages", out var messages) && messages.GetArrayLength() > 0
                ? messages[0].TryGetProperty("id", out var idProperty) ? idProperty.GetString() : null
                : null;
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new InvalidOperationException("Graph API accepted the message but did not return a message ID.");
        }

        return messageId;
    }

    private static string WhatsAppMediaType(string contentType) => contentType switch
    {
        _ when contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) => "image",
        _ when contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) => "video",
        _ when contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) => "audio",
        _ => "document"
    };

    private static string MessengerAttachmentType(string contentType) => contentType switch
    {
        _ when contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) => "image",
        _ when contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) => "video",
        _ when contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) => "audio",
        _ => "file"
    };
}
