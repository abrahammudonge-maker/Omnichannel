using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Domain.Enums;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequireAgent")]
[Route("api/[controller]")]
public sealed class ConversationsController : ControllerBase
{
    private readonly IConversationRepository _conversationRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IUserRepository _userRepository;
    private readonly IChannelAccountRepository _channelAccountRepository;

    public ConversationsController(
        IConversationRepository conversationRepository,
        ICustomerRepository customerRepository,
        IUserRepository userRepository,
        IChannelAccountRepository channelAccountRepository)
    {
        _conversationRepository = conversationRepository;
        _customerRepository = customerRepository;
        _userRepository = userRepository;
        _channelAccountRepository = channelAccountRepository;
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
        if (await _customerRepository.GetByIdAsync(request.CustomerId, organizationId, cancellationToken) is null)
            return BadRequest(ApiResponse<Guid>.Fail("Customer must belong to your organization."));
        if (!Enum.TryParse<Channel>(request.Channel, true, out var channel))
            return BadRequest(ApiResponse<Guid>.Fail("Invalid channel."));
        if (request.Status is not ("Open" or "In Progress" or "Resolved" or "Closed"))
            return BadRequest(ApiResponse<Guid>.Fail("Invalid conversation status."));
        if (request.AssignedUserId is Guid userId && await _userRepository.GetByIdAsync(userId, organizationId, cancellationToken) is null)
            return BadRequest(ApiResponse<Guid>.Fail("Assignee must belong to your organization."));

        Guid? channelAccountId = request.ChannelAccountId;
        if (channelAccountId is Guid requestedAccountId)
        {
            var account = await _channelAccountRepository.GetByIdAsync(requestedAccountId, organizationId, cancellationToken);
            if (account is null || account.ChannelType != request.Channel)
                return BadRequest(ApiResponse<Guid>.Fail("The selected channel account is invalid for this channel."));
        }

        var conversation = new Conversation
        {
            OrganizationId = organizationId,
            CustomerId = request.CustomerId,
            Channel = channel,
            Status = request.Status,
            AssignedUserId = request.AssignedUserId,
            ChannelAccountId = channelAccountId
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
