using Microsoft.AspNetCore.Mvc;
using Omni.Application.Interfaces;

namespace Omni.Api.Controllers;

/// <summary>
/// Receives voice provider webhooks (call status changes, recording availability, inbound calls).
/// Called anonymously by the provider's own servers, not by authenticated app users — tenant identity
/// is always derived from the matched Call/PhoneNumber, never from anything the request claims.
/// </summary>
[ApiController]
[Route("api/webhooks/voice")]
public sealed class VoiceWebhookController : ControllerBase
{
    private readonly IVoiceProviderFactory _voiceProviderFactory;
    private readonly IVoiceService _voiceService;
    private readonly ILogger<VoiceWebhookController> _logger;

    public VoiceWebhookController(IVoiceProviderFactory voiceProviderFactory, IVoiceService voiceService, ILogger<VoiceWebhookController> logger)
    {
        _voiceProviderFactory = voiceProviderFactory;
        _voiceService = voiceService;
        _logger = logger;
    }

    [HttpPost("{provider}")]
    public async Task<IActionResult> Receive(string provider, CancellationToken cancellationToken)
    {
        IVoiceProvider voiceProvider;
        try
        {
            voiceProvider = _voiceProviderFactory.Resolve(provider);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }

        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        var requestUrl = $"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}";
        var validationContext = new WebhookValidationContext(rawBody, requestUrl, headers);

        if (!await voiceProvider.ValidateWebhookAsync(validationContext, cancellationToken))
        {
            _logger.LogWarning("Rejected voice webhook for provider {Provider}: signature validation failed.", provider);
            return Unauthorized();
        }

        try
        {
            var webhookEvent = await voiceProvider.ProcessWebhookAsync(rawBody, cancellationToken);
            if (webhookEvent is not null)
            {
                await _voiceService.ProcessWebhookAsync(provider, webhookEvent, rawBody, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Always acknowledge the webhook once the signature is trusted, or the provider retries
            // aggressively — matches the same tolerant pattern MetaWebhookController uses.
            _logger.LogError(ex, "Failed to process voice webhook for provider {Provider}.", provider);
        }

        return Ok();
    }
}
