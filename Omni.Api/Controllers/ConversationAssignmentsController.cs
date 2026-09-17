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
public sealed class ConversationAssignmentsController : ControllerBase
{
    private readonly IConversationAssignmentRepository _conversationAssignmentRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IUserRepository _userRepository;

    public ConversationAssignmentsController(IConversationAssignmentRepository conversationAssignmentRepository, IConversationRepository conversationRepository, IUserRepository userRepository)
    {
        _conversationAssignmentRepository = conversationAssignmentRepository;
        _conversationRepository = conversationRepository;
        _userRepository = userRepository;
    }

    [HttpGet("conversation/{conversationId:guid}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ConversationAssignment>>>> GetByConversationId(Guid conversationId, CancellationToken cancellationToken)
    {
        var result = await _conversationAssignmentRepository.GetByConversationIdAsync(conversationId, GetOrganizationId(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ConversationAssignment>>.Ok(result, "Conversation assignments retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ConversationAssignment>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _conversationAssignmentRepository.GetByIdAsync(id, GetOrganizationId(), cancellationToken);
        return result is null
            ? NotFound(ApiResponse<ConversationAssignment>.Fail("Conversation assignment not found."))
            : Ok(ApiResponse<ConversationAssignment>.Ok(result, "Conversation assignment retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] AssignConversationRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, organizationId, cancellationToken);
        if (conversation is null)
            return NotFound(ApiResponse<Guid>.Fail("Conversation not found."));

        if (request.AssignedTo is null)
        {
            conversation.AssignedUserId = null;
            await _conversationRepository.UpdateAsync(conversation, cancellationToken);
            return Ok(ApiResponse<Guid>.Ok(Guid.Empty, "Conversation unassigned successfully."));
        }

        if (await _userRepository.GetByIdAsync(request.AssignedTo.Value, organizationId, cancellationToken) is null)
            return BadRequest(ApiResponse<Guid>.Fail("Assignee must belong to your organization."));

        var assignment = new ConversationAssignment
        {
            ConversationId = request.ConversationId,
            AssignedTo = request.AssignedTo.Value,
            AssignedBy = GetUserId(),
            Reason = request.Reason
        };

        var id = await _conversationAssignmentRepository.CreateAsync(assignment, cancellationToken);

        conversation.AssignedUserId = request.AssignedTo.Value;
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);

        return Ok(ApiResponse<Guid>.Ok(id, "Conversation assignment created successfully."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }

    private Guid GetUserId() => Guid.Parse(User.Claims.First(c => c.Type == "UserId").Value);
}
