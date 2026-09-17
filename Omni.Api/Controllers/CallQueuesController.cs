using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Api.Extensions;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequireAgent")]
[Route("api/[controller]")]
public sealed class CallQueuesController : ControllerBase
{
    private readonly ICallQueueRepository _callQueueRepository;
    private readonly ICallQueueMemberRepository _callQueueMemberRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public CallQueuesController(ICallQueueRepository callQueueRepository, ICallQueueMemberRepository callQueueMemberRepository, IAuditLogRepository auditLogRepository)
    {
        _callQueueRepository = callQueueRepository;
        _callQueueMemberRepository = callQueueMemberRepository;
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CallQueue>>>> GetAll(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _callQueueRepository.GetAllAsync(organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CallQueue>>.Ok(result, "Call queues retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CallQueue>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _callQueueRepository.GetByIdAsync(id, organizationId, cancellationToken);
        return result is null
            ? NotFound(ApiResponse<CallQueue>.Fail("Call queue not found."))
            : Ok(ApiResponse<CallQueue>.Ok(result, "Call queue retrieved successfully."));
    }

    [HttpPost]
    [Authorize(Policy = "RequireOrganizationAdmin")]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreateCallQueueRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var queue = new CallQueue
        {
            OrganizationId = organizationId,
            DepartmentId = request.DepartmentId,
            Name = request.Name,
            Description = request.Description,
            Strategy = request.Strategy,
            IsActive = request.IsActive
        };
        var id = await _callQueueRepository.CreateAsync(queue, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Create", "CallQueue", id, cancellationToken, request.Name);
        return Ok(ApiResponse<Guid>.Ok(id, "Call queue created successfully."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireOrganizationAdmin")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id, [FromBody] UpdateCallQueueRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var existing = await _callQueueRepository.GetByIdAsync(id, organizationId, cancellationToken);
        if (existing is null)
        {
            return NotFound(ApiResponse.Fail("Call queue not found."));
        }

        existing.DepartmentId = request.DepartmentId;
        existing.Name = request.Name;
        existing.Description = request.Description;
        existing.Strategy = request.Strategy;
        existing.IsActive = request.IsActive;

        await _callQueueRepository.UpdateAsync(existing, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Update", "CallQueue", id, cancellationToken, request.Name);
        return Ok(ApiResponse.Ok("Call queue updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireOrganizationAdmin")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        await _callQueueRepository.DeleteAsync(id, organizationId, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Delete", "CallQueue", id, cancellationToken);
        return Ok(ApiResponse.Ok("Call queue deleted successfully."));
    }

    [HttpGet("{queueId:guid}/members")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CallQueueMember>>>> GetMembers(Guid queueId, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _callQueueMemberRepository.GetByQueueIdAsync(queueId, organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CallQueueMember>>.Ok(result, "Queue members retrieved successfully."));
    }

    [HttpPost("{queueId:guid}/members")]
    [Authorize(Policy = "RequireOrganizationAdmin")]
    public async Task<ActionResult<ApiResponse<Guid>>> AddMember(Guid queueId, [FromBody] CreateCallQueueMemberRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var queue = await _callQueueRepository.GetByIdAsync(queueId, organizationId, cancellationToken);
        if (queue is null)
        {
            return NotFound(ApiResponse<Guid>.Fail("Call queue not found."));
        }

        var member = new CallQueueMember
        {
            OrganizationId = organizationId,
            QueueId = queueId,
            UserId = request.UserId,
            Priority = request.Priority,
            IsActive = request.IsActive
        };
        var id = await _callQueueMemberRepository.CreateAsync(member, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Create", "CallQueueMember", id, cancellationToken);
        return Ok(ApiResponse<Guid>.Ok(id, "Queue member added successfully."));
    }

    [HttpDelete("{queueId:guid}/members/{memberId:guid}")]
    [Authorize(Policy = "RequireOrganizationAdmin")]
    public async Task<ActionResult<ApiResponse>> RemoveMember(Guid queueId, Guid memberId, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        await _callQueueMemberRepository.DeleteAsync(memberId, organizationId, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Delete", "CallQueueMember", memberId, cancellationToken);
        return Ok(ApiResponse.Ok("Queue member removed successfully."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
