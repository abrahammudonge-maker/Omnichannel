using Microsoft.AspNetCore.Mvc;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;

namespace Omni.Api.Extensions;

public static class AuditLogExtensions
{
    public static Task LogAuditAsync(
        this ControllerBase controller,
        IAuditLogRepository auditLogRepository,
        Guid organizationId,
        string action,
        string entity,
        Guid? entityId,
        CancellationToken cancellationToken,
        string? metadata = null)
    {
        var userIdClaim = controller.User.Claims.FirstOrDefault(c => c.Type == "UserId");
        var auditLog = new AuditLog
        {
            OrganizationId = organizationId,
            UserId = userIdClaim is not null ? Guid.Parse(userIdClaim.Value) : null,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            IpAddress = controller.HttpContext.Connection.RemoteIpAddress?.ToString(),
            Metadata = metadata
        };

        return auditLogRepository.CreateAsync(auditLog, cancellationToken);
    }
}
