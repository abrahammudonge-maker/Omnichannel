using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Omni.Api.Authentication;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Domain.ValueObjects;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

/// <summary>
/// Sends one-time codes over WhatsApp on behalf of another system. The caller passes a number and the
/// code only. The send opens (or reuses) a conversation so the customer can reply, and the code stays
/// visible in that conversation until it expires.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
[Route("api/otp")]
public sealed class OtpController : ControllerBase
{
    public const string TemplateSettingName = "OtpTemplateId";
    public const string ChannelSettingName = "OtpChannelAccountId";
    private const int MaxCodesPerNumberPerTenMinutes = 3;
    private static readonly Regex CodePattern = new(@"^\d{4,8}$");

    private readonly IOtpRepository _otpRepository;
    private readonly IOrganizationSettingRepository _organizationSettingRepository;
    private readonly IMessageTemplateRepository _messageTemplateRepository;
    private readonly IChannelAccountRepository _channelAccountRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly ITemplateMessageService _templateMessageService;
    private readonly IWhatsAppContactResolver _contactResolver;

    public OtpController(
        IOtpRepository otpRepository,
        IOrganizationSettingRepository organizationSettingRepository,
        IMessageTemplateRepository messageTemplateRepository,
        IChannelAccountRepository channelAccountRepository,
        IMessageRepository messageRepository,
        ITemplateMessageService templateMessageService,
        IWhatsAppContactResolver contactResolver)
    {
        _otpRepository = otpRepository;
        _organizationSettingRepository = organizationSettingRepository;
        _messageTemplateRepository = messageTemplateRepository;
        _channelAccountRepository = channelAccountRepository;
        _messageRepository = messageRepository;
        _templateMessageService = templateMessageService;
        _contactResolver = contactResolver;
    }

    [HttpPost("send")]
    [EnableRateLimiting("integrations")]
    public async Task<ActionResult<ApiResponse<SendOtpResponse>>> Send([FromBody] SendOtpRequest request, CancellationToken cancellationToken)
    {
        if (!IsOtpKey()) return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<SendOtpResponse>.Fail("This API key isn't an OTP key."));

        var organizationId = GetOrganizationId();
        var number = WhatsAppNumber.NormalizeForSending(request.PhoneNumber);
        if (number is null) return BadRequest(ApiResponse<SendOtpResponse>.Fail("Enter a valid WhatsApp number with its country code."));
        if (request.Code is null || !CodePattern.IsMatch(request.Code)) return BadRequest(ApiResponse<SendOtpResponse>.Fail("The code must be 4 to 8 digits."));
        if (request.ExpiresInMinutes is < 1 or > 15) return BadRequest(ApiResponse<SendOtpResponse>.Fail("expiresInMinutes must be between 1 and 15."));

        var now = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await _otpRepository.FindByIdempotencyKeyAsync(organizationId, request.IdempotencyKey, now.AddHours(-24), cancellationToken);
            if (existing is not null)
            {
                return Ok(ApiResponse<SendOtpResponse>.Ok(ToResponse(existing, replayed: true, error: null), "This request was already processed."));
            }
        }

        var recentCount = await _otpRepository.CountSinceForPhoneAsync(organizationId, number, now.AddMinutes(-10), cancellationToken);
        if (recentCount >= MaxCodesPerNumberPerTenMinutes)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, ApiResponse<SendOtpResponse>.Fail("Too many codes were sent to this number in the last 10 minutes. Try again shortly."));
        }

        var settings = await _organizationSettingRepository.GetAllAsync(organizationId, cancellationToken);
        if (!Guid.TryParse(SettingValue(settings, TemplateSettingName), out var templateId))
        {
            return BadRequest(ApiResponse<SendOtpResponse>.Fail("No OTP template is configured for this organization."));
        }

        var template = await _messageTemplateRepository.GetByIdAsync(templateId, organizationId, cancellationToken);
        if (template is null
            || !string.Equals(template.Status, "APPROVED", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(template.Category, "AUTHENTICATION", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse<SendOtpResponse>.Fail("The configured OTP template must be an approved AUTHENTICATION template."));
        }

        // A key tied to a number always sends from it; an untied key keeps the old choice order.
        var boundAccountId = NumberKeyChannelResolver.BoundChannelAccountId(User);
        if (boundAccountId is not null && request.ChannelAccountId is not null && request.ChannelAccountId != boundAccountId)
        {
            return BadRequest(ApiResponse<SendOtpResponse>.Fail("This API key can only send from the WhatsApp number it's tied to."));
        }

        var accountId = boundAccountId
            ?? request.ChannelAccountId
            ?? (Guid.TryParse(SettingValue(settings, ChannelSettingName), out var configured) ? configured : template.ChannelAccountId);
        var account = await _channelAccountRepository.GetByIdAsync(accountId, organizationId, cancellationToken);
        if (account is null || !NumberKeyChannelResolver.IsUsable(account))
        {
            return BadRequest(ApiResponse<SendOtpResponse>.Fail("The selected sending number isn't a connected, active WhatsApp number."));
        }

        var customer = await _contactResolver.FindOrCreateCustomerAsync(organizationId, number, null, cancellationToken);
        var conversation = await _contactResolver.FindOrCreateConversationAsync(organizationId, customer.Id, account.Id, cancellationToken);

        var send = await _templateMessageService.SendAsync(
            organizationId, conversation.Id, account, template, number, new[] { request.Code }, cancellationToken);

        var message = await _messageRepository.GetByIdAsync(send.MessageId, organizationId, cancellationToken);
        if (message is not null)
        {
            message.MessageType = "Otp";
            await _messageRepository.UpdateAsync(message, cancellationToken);
        }

        var otp = new OtpMessage
        {
            OrganizationId = organizationId,
            ConversationId = conversation.Id,
            MessageId = send.MessageId,
            ChannelAccountId = account.Id,
            PhoneNumber = number,
            Purpose = request.Purpose,
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey,
            ExternalMessageId = send.ExternalMessageId,
            Status = send.Success ? "Sent" : "Failed",
            ExpiresAt = now.AddMinutes(request.ExpiresInMinutes),
            CreatedAt = now
        };
        await _otpRepository.CreateAsync(otp, cancellationToken);

        return Ok(ApiResponse<SendOtpResponse>.Ok(
            ToResponse(otp, replayed: false, error: send.ErrorMessage),
            send.Success ? "Code accepted for delivery." : "Code was not delivered."));
    }

    [HttpGet("{otpId:guid}")]
    public async Task<ActionResult<ApiResponse<OtpStatusView>>> GetStatus(Guid otpId, CancellationToken cancellationToken)
    {
        if (!IsOtpKey()) return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<OtpStatusView>.Fail("This API key isn't an OTP key."));

        var otp = await _otpRepository.FindByIdAsync(otpId, GetOrganizationId(), cancellationToken);
        if (otp is null) return NotFound(ApiResponse<OtpStatusView>.Fail("OTP not found."));

        return Ok(ApiResponse<OtpStatusView>.Ok(
            new OtpStatusView(otp.Id, otp.PhoneNumber, otp.Purpose, otp.Status, otp.ConversationId, otp.CreatedAt, otp.ExpiresAt),
            "OTP status retrieved."));
    }

    private static SendOtpResponse ToResponse(OtpMessage otp, bool replayed, string? error) =>
        new(otp.Id, otp.Status == "Sent", otp.Status, otp.ConversationId, otp.ExpiresAt, replayed, error);

    private static string? SettingValue(IReadOnlyList<OrganizationSetting> settings, string name) =>
        settings.FirstOrDefault(s => string.Equals(s.SettingName, name, StringComparison.OrdinalIgnoreCase))?.SettingValue;

    // "whatsapp" keys may send OTPs too; "otp" keys can't reach the general template endpoints.
    private bool IsOtpKey() => NumberKeyChannelResolver.IsNumberKey(User);

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
