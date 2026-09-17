using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequireOrganizationAdmin")]
[Route("api/[controller]")]
public sealed class AuditLogsController : ControllerBase
{
    private readonly IAuditLogRepository _auditLogRepository;

    public AuditLogsController(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditLog>>>> GetAll(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _auditLogRepository.GetAllAsync(organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AuditLog>>.Ok(result, "Audit logs retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<AuditLog>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _auditLogRepository.GetByIdAsync(id, organizationId, cancellationToken);
        return result is null
            ? NotFound(ApiResponse<AuditLog>.Fail("Audit log not found."))
            : Ok(ApiResponse<AuditLog>.Ok(result, "Audit log retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreateAuditLogRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var auditLog = new AuditLog
        {
            OrganizationId = organizationId,
            UserId = GetUserId(),
            Action = request.Action,
            Entity = request.Entity,
            EntityId = request.EntityId,
            IpAddress = request.IpAddress,
            Metadata = request.Metadata
        };

        var id = await _auditLogRepository.CreateAsync(auditLog, cancellationToken);
        return Ok(ApiResponse<Guid>.Ok(id, "Audit log created successfully."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }

    private Guid GetUserId() => Guid.Parse(User.Claims.First(c => c.Type == "UserId").Value);
}
