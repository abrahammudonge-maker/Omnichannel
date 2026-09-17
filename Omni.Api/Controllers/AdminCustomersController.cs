using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Api.Extensions;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequirePlatformSuperAdmin")]
[Route("api/admin/customers")]
public sealed class AdminCustomersController : ControllerBase
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public AdminCustomersController(ICustomerRepository customerRepository, IOrganizationRepository organizationRepository, IAuditLogRepository auditLogRepository)
    {
        _customerRepository = customerRepository;
        _organizationRepository = organizationRepository;
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminCustomerView>>>> GetAll(CancellationToken cancellationToken)
    {
        var customers = await _customerRepository.GetAllAsync(cancellationToken);
        var organizations = await _organizationRepository.GetAllAsync(cancellationToken);
        var organizationNames = organizations.ToDictionary(o => o.Id, o => o.Name);

        var result = customers
            .Select(c => new AdminCustomerView(
                c.Id,
                c.OrganizationId,
                organizationNames.GetValueOrDefault(c.OrganizationId, "Unknown"),
                c.FullName,
                c.Phone,
                c.Email,
                c.FacebookId,
                c.InstagramId,
                c.WhatsAppNumber,
                c.CreatedAt))
            .ToList();

        return Ok(ApiResponse<IReadOnlyList<AdminCustomerView>>.Ok(result, "Customers retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] AdminCreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var organization = await _organizationRepository.GetByIdAsync(request.OrganizationId, cancellationToken);
        if (organization is null) return BadRequest(ApiResponse<Guid>.Fail("Organization not found."));

        var customer = new Customer
        {
            OrganizationId = request.OrganizationId,
            FullName = request.FullName,
            Phone = request.Phone,
            Email = request.Email,
            FacebookId = request.FacebookId,
            InstagramId = request.InstagramId,
            WhatsAppNumber = request.WhatsAppNumber
        };

        var id = await _customerRepository.CreateAsync(customer, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, request.OrganizationId, "AdminCreate", "Customer", id, cancellationToken, $"Created customer {request.FullName}");
        return Ok(ApiResponse<Guid>.Ok(id, "Customer created successfully."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id, [FromBody] AdminUpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken);
        if (customer is null) return NotFound(ApiResponse.Fail("Customer not found."));

        customer.FullName = request.FullName;
        customer.Phone = request.Phone;
        customer.Email = request.Email;
        customer.FacebookId = request.FacebookId;
        customer.InstagramId = request.InstagramId;
        customer.WhatsAppNumber = request.WhatsAppNumber;

        await _customerRepository.UpdateAsync(customer, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, customer.OrganizationId, "AdminUpdate", "Customer", id, cancellationToken, $"Updated customer {request.FullName}");
        return Ok(ApiResponse.Ok("Customer updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken);
        if (customer is null) return NotFound(ApiResponse.Fail("Customer not found."));

        await _customerRepository.DeleteAsync(id, customer.OrganizationId, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, customer.OrganizationId, "AdminDelete", "Customer", id, cancellationToken, $"Deleted customer {customer.FullName}");
        return Ok(ApiResponse.Ok("Customer deleted successfully."));
    }
}
