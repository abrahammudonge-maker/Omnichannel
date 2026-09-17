using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequireAgent")]
[Route("api/[controller]")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationRepository _notificationRepository;

    public NotificationsController(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Notification>>>> GetByUserId(Guid userId, CancellationToken cancellationToken)
    {
        if (userId != GetUserId() && !User.IsInRole("OrganizationAdmin") && !User.IsInRole("PlatformSuperAdmin"))
            return Forbid();
        var organizationId = GetOrganizationId();
        var result = await _notificationRepository.GetByUserIdAsync(userId, organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<Notification>>.Ok(result, "Notifications retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreateNotificationRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var notification = new Notification
        {
            OrganizationId = organizationId,
            UserId = request.UserId,
            ConversationId = request.ConversationId,
            Title = request.Title,
            Message = request.Message,
            IsRead = false
        };

        var id = await _notificationRepository.CreateAsync(notification, cancellationToken);
        return Ok(ApiResponse<Guid>.Ok(id, "Notification created successfully."));
    }

    [HttpPost("{id:guid}/read")]
    public async Task<ActionResult<ApiResponse>> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        await _notificationRepository.MarkAsReadAsync(id, organizationId, cancellationToken);
        return Ok(ApiResponse.Ok("Notification marked as read."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }

    private Guid GetUserId() => Guid.Parse(User.Claims.First(c => c.Type == "UserId").Value);
}
