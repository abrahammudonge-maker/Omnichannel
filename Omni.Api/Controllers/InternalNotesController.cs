using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class InternalNotesController : ControllerBase
{
    private readonly IInternalNoteRepository _internalNoteRepository;

    public InternalNotesController(IInternalNoteRepository internalNoteRepository)
    {
        _internalNoteRepository = internalNoteRepository;
    }

    [HttpGet("conversation/{conversationId:guid}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<InternalNote>>>> GetByConversationId(Guid conversationId, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _internalNoteRepository.GetByConversationIdAsync(conversationId, organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<InternalNote>>.Ok(result, "Internal notes retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] AddInternalNoteRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var userId = GetUserId();
        var note = new InternalNote
        {
            OrganizationId = organizationId,
            ConversationId = request.ConversationId,
            UserId = userId,
            Body = request.Body
        };

        var id = await _internalNoteRepository.CreateAsync(note, cancellationToken);
        return Ok(ApiResponse<Guid>.Ok(id, "Internal note added successfully."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id, [FromBody] UpdateInternalNoteRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var existing = await _internalNoteRepository.GetByIdAsync(id, organizationId, cancellationToken);
        if (existing is null)
            return NotFound(ApiResponse.Fail("Internal note not found."));

        existing.Body = request.Body;
        await _internalNoteRepository.UpdateAsync(existing, cancellationToken);
        return Ok(ApiResponse.Ok("Internal note updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        await _internalNoteRepository.DeleteAsync(id, organizationId, cancellationToken);
        return Ok(ApiResponse.Ok("Internal note deleted successfully."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }

    private Guid GetUserId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
