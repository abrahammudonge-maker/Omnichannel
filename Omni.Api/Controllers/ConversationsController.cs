using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Domain.Enums;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class ConversationsController : ControllerBase
{
    private readonly IConversationRepository _conversationRepository;

    public ConversationsController(IConversationRepository conversationRepository)
    {
        _conversationRepository = conversationRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Conversation>>>> GetAll(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _conversationRepository.GetAllAsync(organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<Conversation>>.Ok(result, "Conversations retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<Conversation>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _conversationRepository.GetByIdAsync(id, organizationId, cancellationToken);
        return result is null
            ? NotFound(ApiResponse<Conversation>.Fail("Conversation not found."))
            : Ok(ApiResponse<Conversation>.Ok(result, "Conversation retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreateConversationRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var conversation = new Conversation
        {
            OrganizationId = organizationId,
            CustomerId = request.CustomerId,
            Channel = Enum.Parse<Channel>(request.Channel),
            Status = request.Status,
            AssignedUserId = request.AssignedUserId
        };

        var id = await _conversationRepository.CreateAsync(conversation, cancellationToken);
        return Ok(ApiResponse<Guid>.Ok(id, "Conversation created successfully."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
