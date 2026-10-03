using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Enums;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequireAgent")]
[Route("api/[controller]")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IMessageRepository _messageRepository;

    public AnalyticsController(IMessageRepository messageRepository)
    {
        _messageRepository = messageRepository;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<MessagingSummaryView>>> GetSummary([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var organizationId = GetOrganizationId();
        var windowDays = Math.Clamp(days, 1, 365);
        var rows = await _messageRepository.GetStatsSinceAsync(organizationId, DateTimeOffset.UtcNow.AddDays(-windowDays), cancellationToken);

        var outbound = rows.Where(r => r.Direction == "Outbound").ToList();
        var inbound = rows.Where(r => r.Direction == "Inbound").ToList();
        var delivered = outbound.Where(r => r.Status is "delivered" or "read").Sum(r => r.Count);
        var sentOutbound = outbound.Sum(r => r.Count);

        var byChannel = rows
            .GroupBy(r => r.Channel)
            .Select(g => new ChannelSummaryView(
                ((Channel)g.Key).ToString(),
                g.Where(r => r.Direction == "Inbound").Sum(r => r.Count),
                g.Where(r => r.Direction == "Outbound").Sum(r => r.Count),
                g.Where(r => r.Status == "failed").Sum(r => r.Count)))
            .OrderByDescending(c => c.Inbound + c.Outbound)
            .ToList();

        var daily = rows
            .GroupBy(r => DateOnly.FromDateTime(r.Day))
            .OrderBy(g => g.Key)
            .Select(g => new DailySummaryView(
                g.Key,
                g.Where(r => r.Direction == "Inbound").Sum(r => r.Count),
                g.Where(r => r.Direction == "Outbound").Sum(r => r.Count),
                g.Where(r => r.Status == "failed").Sum(r => r.Count)))
            .ToList();

        var summary = new MessagingSummaryView(
            windowDays,
            inbound.Sum(r => r.Count),
            sentOutbound,
            delivered,
            rows.Where(r => r.Status == "failed").Sum(r => r.Count),
            sentOutbound == 0 ? 0 : Math.Round(delivered * 100.0 / sentOutbound, 1),
            byChannel,
            daily);

        return Ok(ApiResponse<MessagingSummaryView>.Ok(summary, "Analytics retrieved successfully."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
