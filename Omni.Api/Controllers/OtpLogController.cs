using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Application.Interfaces;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

public sealed record OtpLogEntryView(Guid Id, string MaskedPhoneNumber, string? Purpose, string Status, Guid? ConversationId, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, bool IsExpired);

/// <summary>Staff-facing log of every OTP sent through the OTP endpoint. Phone numbers are masked.</summary>
[ApiController]
[Authorize(Policy = "RequireAgent")]
[Route("api/otp-log")]
public sealed class OtpLogController : ControllerBase
{
    private readonly IOtpRepository _otpRepository;

    public OtpLogController(IOtpRepository otpRepository)
    {
        _otpRepository = otpRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OtpLogEntryView>>>> GetLog([FromQuery] int limit = 100, CancellationToken cancellationToken = default)
    {
        var rows = await _otpRepository.GetRecentAsync(GetOrganizationId(), Math.Clamp(limit, 1, 500), cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var view = rows.Select(o => new OtpLogEntryView(
            o.Id, Mask(o.PhoneNumber), o.Purpose, o.Status, o.ConversationId, o.CreatedAt, o.ExpiresAt, o.ExpiresAt <= now)).ToList();
        return Ok(ApiResponse<IReadOnlyList<OtpLogEntryView>>.Ok(view, "OTP log retrieved."));
    }

    private static string Mask(string number) =>
        number.Length <= 4 ? "••••" : $"{number[..Math.Min(4, number.Length - 3)]}•••{number[^3..]}";

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
