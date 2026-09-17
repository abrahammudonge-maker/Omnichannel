using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequireAgent")]
[Route("api/conversations/{conversationId:guid}/tags")]
public sealed class ConversationTagsController : ControllerBase
{
    private readonly IConversationRepository _conversationRepository;
    private readonly ITagRepository _tagRepository;
    private readonly IConversationTagRepository _conversationTagRepository;
    public ConversationTagsController(IConversationRepository conversationRepository, ITagRepository tagRepository, IConversationTagRepository conversationTagRepository)
        => (_conversationRepository, _tagRepository, _conversationTagRepository) = (conversationRepository, tagRepository, conversationTagRepository);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Guid>>>> Get(Guid conversationId, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        if (await _conversationRepository.GetByIdAsync(conversationId, organizationId, cancellationToken) is null)
            return NotFound(ApiResponse<IReadOnlyList<Guid>>.Fail("Conversation not found."));
        var result = await _conversationTagRepository.GetTagIdsAsync(conversationId, organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<Guid>>.Ok(result));
    }

    [HttpPut]
    public async Task<ActionResult<ApiResponse>> Replace(Guid conversationId, [FromBody] ReplaceConversationTagsRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        if (await _conversationRepository.GetByIdAsync(conversationId, organizationId, cancellationToken) is null)
            return NotFound(ApiResponse.Fail("Conversation not found."));
        foreach (var tagId in request.TagIds.Distinct())
            if (await _tagRepository.GetByIdAsync(tagId, organizationId, cancellationToken) is null)
                return BadRequest(ApiResponse.Fail("Every tag must belong to your organization."));
        await _conversationTagRepository.ReplaceAsync(conversationId, organizationId, request.TagIds, cancellationToken);
        return Ok(ApiResponse.Ok("Conversation tags updated successfully."));
    }

    private Guid GetOrganizationId() => Guid.Parse(User.Claims.First(c => c.Type == "OrganizationId").Value);
}
