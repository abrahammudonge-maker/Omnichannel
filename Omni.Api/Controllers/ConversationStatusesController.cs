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
public sealed class ConversationStatusesController : ControllerBase
{
    private readonly IConversationStatusRepository _conversationStatusRepository;
    private readonly IConversationRepository _conversationRepository;

    public ConversationStatusesController(IConversationStatusRepository conversationStatusRepository, IConversationRepository conversationRepository)
    {
        _conversationStatusRepository = conversationStatusRepository;
        _conversationRepository = conversationRepository;
    }

    [HttpGet("conversation/{conversationId:guid}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ConversationStatusHistory>>>> GetByConversationId(Guid conversationId, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _conversationStatusRepository.GetByConversationIdAsync(conversationId, organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ConversationStatusHistory>>.Ok(result, "Conversation status history retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] UpdateConversationStatusRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var statusHistory = new ConversationStatusHistory
        {
            OrganizationId = organizationId,
            ConversationId = request.ConversationId,
            Status = request.Status,
            ChangedBy = request.ChangedBy,
            Reason = request.Reason
        };

        var id = await _conversationStatusRepository.CreateAsync(statusHistory, cancellationToken);

        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, organizationId, cancellationToken);
        if (conversation is not null)
        {
            conversation.Status = request.Status;
            await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        }

        return Ok(ApiResponse<Guid>.Ok(id, "Conversation status updated successfully."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
