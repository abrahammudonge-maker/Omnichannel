using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Api.Extensions;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Enums;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequirePlatformSuperAdmin")]
[Route("api/admin/conversations")]
public sealed class AdminConversationsController : ControllerBase
{
    private readonly IConversationRepository _conversationRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public AdminConversationsController(
        IConversationRepository conversationRepository,
        ICustomerRepository customerRepository,
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IAuditLogRepository auditLogRepository)
    {
        _conversationRepository = conversationRepository;
        _customerRepository = customerRepository;
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminConversationView>>>> GetAll(CancellationToken cancellationToken)
    {
        var conversations = await _conversationRepository.GetAllAsync(cancellationToken);
        var organizations = await _organizationRepository.GetAllAsync(cancellationToken);
        var customers = await _customerRepository.GetAllAsync(cancellationToken);
        var users = await _userRepository.GetAllAsync(cancellationToken);

        var organizationNames = organizations.ToDictionary(o => o.Id, o => o.Name);
        var customerNames = customers.ToDictionary(c => c.Id, c => c.FullName);
        var userNames = users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}");

        var result = conversations
            .Select(c => new AdminConversationView(
                c.Id,
                c.OrganizationId,
                organizationNames.GetValueOrDefault(c.OrganizationId, "Unknown"),
                c.CustomerId,
                customerNames.GetValueOrDefault(c.CustomerId, "Unknown"),
                c.Channel.ToString(),
                c.Status,
                c.AssignedUserId,
                c.AssignedUserId is Guid assignedUserId ? userNames.GetValueOrDefault(assignedUserId, "Unknown") : null,
                c.CreatedAt))
            .ToList();

        return Ok(ApiResponse<IReadOnlyList<AdminConversationView>>.Ok(result, "Conversations retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] AdminCreateConversationRequest request, CancellationToken cancellationToken)
    {
        var organization = await _organizationRepository.GetByIdAsync(request.OrganizationId, cancellationToken);
        if (organization is null) return BadRequest(ApiResponse<Guid>.Fail("Organization not found."));

        if (await _customerRepository.GetByIdAsync(request.CustomerId, request.OrganizationId, cancellationToken) is null)
            return BadRequest(ApiResponse<Guid>.Fail("Customer must belong to the selected organization."));
        if (!Enum.TryParse<Channel>(request.Channel, true, out var channel))
            return BadRequest(ApiResponse<Guid>.Fail("Invalid channel."));
        if (request.Status is not ("Open" or "In Progress" or "Resolved" or "Closed"))
            return BadRequest(ApiResponse<Guid>.Fail("Invalid conversation status."));
        if (request.AssignedUserId is Guid userId && await _userRepository.GetByIdAsync(userId, request.OrganizationId, cancellationToken) is null)
            return BadRequest(ApiResponse<Guid>.Fail("Assignee must belong to the selected organization."));

        var conversation = new Omni.Domain.Entities.Conversation
        {
            OrganizationId = request.OrganizationId,
            CustomerId = request.CustomerId,
            Channel = channel,
            Status = request.Status,
            AssignedUserId = request.AssignedUserId
        };

        var id = await _conversationRepository.CreateAsync(conversation, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, request.OrganizationId, "AdminCreate", "Conversation", id, cancellationToken, $"Created {request.Channel} conversation, status {request.Status}");
        return Ok(ApiResponse<Guid>.Ok(id, "Conversation created successfully."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id, [FromBody] AdminUpdateConversationRequest request, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(id, cancellationToken);
        if (conversation is null) return NotFound(ApiResponse.Fail("Conversation not found."));

        if (request.Status is not ("Open" or "In Progress" or "Resolved" or "Closed"))
            return BadRequest(ApiResponse.Fail("Invalid conversation status."));
        if (request.AssignedUserId is Guid userId && await _userRepository.GetByIdAsync(userId, conversation.OrganizationId, cancellationToken) is null)
            return BadRequest(ApiResponse.Fail("Assignee must belong to this conversation's organization."));

        conversation.Status = request.Status;
        conversation.AssignedUserId = request.AssignedUserId;

        await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, conversation.OrganizationId, "AdminUpdate", "Conversation", id, cancellationToken, $"Status set to {request.Status}");
        return Ok(ApiResponse.Ok("Conversation updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(id, cancellationToken);
        if (conversation is null) return NotFound(ApiResponse.Fail("Conversation not found."));

        await _conversationRepository.DeleteAsync(id, conversation.OrganizationId, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, conversation.OrganizationId, "AdminDelete", "Conversation", id, cancellationToken);
        return Ok(ApiResponse.Ok("Conversation deleted successfully."));
    }
}
