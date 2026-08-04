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
public sealed class ConversationAssignmentsController : ControllerBase
{
    private readonly IConversationAssignmentRepository _conversationAssignmentRepository;
    private readonly IConversationRepository _conversationRepository;

    public ConversationAssignmentsController(IConversationAssignmentRepository conversationAssignmentRepository, IConversationRepository conversationRepository)
    {
        _conversationAssignmentRepository = conversationAssignmentRepository;
        _conversationRepository = conversationRepository;
    }

    [HttpGet("conversation/{conversationId:guid}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ConversationAssignment>>>> GetByConversationId(Guid conversationId, CancellationToken cancellationToken)
    {
        var result = await _conversationAssignmentRepository.GetByConversationIdAsync(conversationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ConversationAssignment>>.Ok(result, "Conversation assignments retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ConversationAssignment>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _conversationAssignmentRepository.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponse<ConversationAssignment>.Fail("Conversation assignment not found."))
            : Ok(ApiResponse<ConversationAssignment>.Ok(result, "Conversation assignment retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] AssignConversationRequest request, CancellationToken cancellationToken)
    {
        var assignment = new ConversationAssignment
        {
            ConversationId = request.ConversationId,
            AssignedTo = request.AssignedTo,
            AssignedBy = request.AssignedBy,
            Reason = request.Reason
        };

        var id = await _conversationAssignmentRepository.CreateAsync(assignment, cancellationToken);

        var organizationId = GetOrganizationId();
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, organizationId, cancellationToken);
        if (conversation is not null)
        {
            conversation.AssignedUserId = request.AssignedTo;
            await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        }

        return Ok(ApiResponse<Guid>.Ok(id, "Conversation assignment created successfully."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
