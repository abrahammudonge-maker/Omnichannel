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
public sealed class AttachmentsController : ControllerBase
{
    private readonly IAttachmentRepository _attachmentRepository;

    public AttachmentsController(IAttachmentRepository attachmentRepository)
    {
        _attachmentRepository = attachmentRepository;
    }

    [HttpGet("conversation/{conversationId:guid}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Attachment>>>> GetByConversationId(Guid conversationId, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _attachmentRepository.GetByConversationIdAsync(conversationId, organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<Attachment>>.Ok(result, "Attachments retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<Attachment>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _attachmentRepository.GetByIdAsync(id, organizationId, cancellationToken);
        return result is null
            ? NotFound(ApiResponse<Attachment>.Fail("Attachment not found."))
            : Ok(ApiResponse<Attachment>.Ok(result, "Attachment retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] UploadAttachmentRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var attachment = new Attachment
        {
            OrganizationId = organizationId,
            ConversationId = request.ConversationId,
            FileName = request.FileName,
            ContentType = request.ContentType,
            FileSize = request.FileSize,
            StoragePath = request.StoragePath,
            UploadedBy = request.UploadedBy
        };

        var id = await _attachmentRepository.CreateAsync(attachment, cancellationToken);
        return Ok(ApiResponse<Guid>.Ok(id, "Attachment uploaded successfully."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        await _attachmentRepository.DeleteAsync(id, organizationId, cancellationToken);
        return Ok(ApiResponse.Ok("Attachment deleted successfully."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
