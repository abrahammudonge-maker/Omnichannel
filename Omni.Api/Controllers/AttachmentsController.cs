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
public sealed class AttachmentsController : ControllerBase
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IWebHostEnvironment _environment;

    public AttachmentsController(IAttachmentRepository attachmentRepository, IConversationRepository conversationRepository, IWebHostEnvironment environment)
    {
        _attachmentRepository = attachmentRepository;
        _conversationRepository = conversationRepository;
        _environment = environment;
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
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromForm] Guid conversationId, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        if (file is null || file.Length == 0 || file.Length > MaxUploadBytes)
            return BadRequest(ApiResponse<Guid>.Fail("Choose a file up to 10 MB."));
        if (await _conversationRepository.GetByIdAsync(conversationId, organizationId, cancellationToken) is null)
            return NotFound(ApiResponse<Guid>.Fail("Conversation not found."));

        var attachmentId = Guid.NewGuid();
        var root = GetUploadsRoot(organizationId);
        Directory.CreateDirectory(root);
        var storagePath = Path.Combine(root, attachmentId.ToString("N"));
        await using (var stream = System.IO.File.Create(storagePath))
            await file.CopyToAsync(stream, cancellationToken);

        var attachment = new Attachment
        {
            Id = attachmentId,
            OrganizationId = organizationId,
            ConversationId = conversationId,
            FileName = Path.GetFileName(file.FileName),
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            FileSize = file.Length,
            StoragePath = storagePath,
            UploadedBy = GetUserId()
        };

        var id = await _attachmentRepository.CreateAsync(attachment, cancellationToken);
        return Ok(ApiResponse<Guid>.Ok(id, "Attachment uploaded successfully."));
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetByIdAsync(id, GetOrganizationId(), cancellationToken);
        if (attachment is null || !IsManagedStoragePath(attachment.StoragePath, attachment.OrganizationId) || !System.IO.File.Exists(attachment.StoragePath)) return NotFound();
        return PhysicalFile(attachment.StoragePath, attachment.ContentType, attachment.FileName);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var attachment = await _attachmentRepository.GetByIdAsync(id, organizationId, cancellationToken);
        if (attachment is not null && IsManagedStoragePath(attachment.StoragePath, attachment.OrganizationId) && System.IO.File.Exists(attachment.StoragePath)) System.IO.File.Delete(attachment.StoragePath);
        await _attachmentRepository.DeleteAsync(id, organizationId, cancellationToken);
        return Ok(ApiResponse.Ok("Attachment deleted successfully."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }

    private Guid GetUserId() => Guid.Parse(User.Claims.First(c => c.Type == "UserId").Value);

    private string GetUploadsRoot(Guid organizationId) => Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "App_Data", "uploads", organizationId.ToString("N")));

    private bool IsManagedStoragePath(string storagePath, Guid organizationId)
    {
        var root = GetUploadsRoot(organizationId) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(storagePath).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
