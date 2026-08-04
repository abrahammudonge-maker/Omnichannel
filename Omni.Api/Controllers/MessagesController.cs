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
public sealed class MessagesController : ControllerBase
{
    private readonly IMessageRepository _messageRepository;

    public MessagesController(IMessageRepository messageRepository)
    {
        _messageRepository = messageRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Message>>>> GetAll(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _messageRepository.GetAllAsync(organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<Message>>.Ok(result, "Messages retrieved successfully."));
    }

    [HttpGet("conversation/{conversationId:guid}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Message>>>> GetByConversationId(Guid conversationId, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _messageRepository.GetByConversationIdAsync(conversationId, organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<Message>>.Ok(result, "Messages retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreateMessageRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var message = new Message
        {
            OrganizationId = organizationId,
            ConversationId = request.ConversationId,
            Direction = request.Direction,
            MessageType = request.MessageType,
            Body = request.Body,
            AttachmentUrl = request.AttachmentUrl,
            Status = request.Status
        };

        var id = await _messageRepository.CreateAsync(message, cancellationToken);
        return Ok(ApiResponse<Guid>.Ok(id, "Message created successfully."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
