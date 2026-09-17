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
public sealed class CallsController : ControllerBase
{
    private readonly ICallRepository _callRepository;
    private readonly IVoiceService _voiceService;

    public CallsController(ICallRepository callRepository, IVoiceService voiceService)
    {
        _callRepository = callRepository;
        _voiceService = voiceService;
    }

    /// <summary>Gets every call belonging to the authenticated organization.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Call>>>> GetAll(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _callRepository.GetAllAsync(organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<Call>>.Ok(result, "Calls retrieved successfully."));
    }

    /// <summary>Gets one call's details, including timing, status, and recording info.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<Call>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _callRepository.GetByIdAsync(id, organizationId, cancellationToken);
        return result is null
            ? NotFound(ApiResponse<Call>.Fail("Call not found."))
            : Ok(ApiResponse<Call>.Ok(result, "Call retrieved successfully."));
    }

    /// <summary>Gets a customer's call history — feeds the customer timeline alongside their messages.</summary>
    [HttpGet("customer/{customerId:guid}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Call>>>> GetByCustomerId(Guid customerId, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _callRepository.GetByCustomerIdAsync(customerId, organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<Call>>.Ok(result, "Calls retrieved successfully."));
    }

    /// <summary>Gets the calls associated with a specific conversation.</summary>
    [HttpGet("conversation/{conversationId:guid}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Call>>>> GetByConversationId(Guid conversationId, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _callRepository.GetByConversationIdAsync(conversationId, organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<Call>>.Ok(result, "Calls retrieved successfully."));
    }

    /// <summary>Initiates an outbound call from one of the organization's phone numbers to a known customer.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<Call>>> Initiate([FromBody] InitiateCallApiRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var agentId = GetUserId();
        var result = await _voiceService.InitiateCallAsync(organizationId, agentId, request.CustomerId, request.PhoneNumberId, request.ConversationId, cancellationToken);
        return ToActionResult(result, "Call initiated successfully.");
    }

    /// <summary>Ends an in-progress call.</summary>
    [HttpPost("{id:guid}/hangup")]
    public async Task<ActionResult<ApiResponse<Call>>> Hangup(Guid id, CancellationToken cancellationToken)
    {
        var result = await _voiceService.HangupAsync(GetOrganizationId(), id, cancellationToken);
        return ToActionResult(result, "Call ended.");
    }

    /// <summary>Puts an in-progress call on hold.</summary>
    [HttpPost("{id:guid}/hold")]
    public async Task<ActionResult<ApiResponse<Call>>> Hold(Guid id, CancellationToken cancellationToken)
    {
        var result = await _voiceService.HoldAsync(GetOrganizationId(), id, cancellationToken);
        return ToActionResult(result, "Call put on hold.");
    }

    /// <summary>Resumes a call that was on hold.</summary>
    [HttpPost("{id:guid}/resume")]
    public async Task<ActionResult<ApiResponse<Call>>> Resume(Guid id, CancellationToken cancellationToken)
    {
        var result = await _voiceService.ResumeAsync(GetOrganizationId(), id, cancellationToken);
        return ToActionResult(result, "Call resumed.");
    }

    /// <summary>Transfers an in-progress call to another destination (e.g. another agent or queue, provider-dependent).</summary>
    [HttpPost("{id:guid}/transfer")]
    public async Task<ActionResult<ApiResponse<Call>>> Transfer(Guid id, [FromBody] TransferCallRequest request, CancellationToken cancellationToken)
    {
        var result = await _voiceService.TransferAsync(GetOrganizationId(), id, request.Destination, cancellationToken);
        return ToActionResult(result, "Call transferred.");
    }

    /// <summary>Starts recording an in-progress call.</summary>
    [HttpPost("{id:guid}/recording/start")]
    public async Task<ActionResult<ApiResponse<Call>>> StartRecording(Guid id, CancellationToken cancellationToken)
    {
        var result = await _voiceService.StartRecordingAsync(GetOrganizationId(), id, cancellationToken);
        return ToActionResult(result, "Recording started.");
    }

    /// <summary>Stops recording an in-progress call.</summary>
    [HttpPost("{id:guid}/recording/stop")]
    public async Task<ActionResult<ApiResponse<Call>>> StopRecording(Guid id, CancellationToken cancellationToken)
    {
        var result = await _voiceService.StopRecordingAsync(GetOrganizationId(), id, cancellationToken);
        return ToActionResult(result, "Recording stopped.");
    }

    private ActionResult<ApiResponse<Call>> ToActionResult(CallActionResult result, string successMessage)
    {
        if (result.NotFound)
        {
            return NotFound(ApiResponse<Call>.Fail(result.ErrorMessage ?? "Not found."));
        }
        if (!result.Success)
        {
            // 200 with a failed envelope, matching how outbound message send failures are reported
            // elsewhere — the Call row itself was still created/recorded even though this action failed.
            return Ok(ApiResponse<Call>.Fail(result.ErrorMessage ?? "The request failed."));
        }
        return Ok(ApiResponse<Call>.Ok(result.Call!, successMessage));
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
