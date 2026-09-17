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
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerRepository _customerRepository;

    public CustomersController(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Customer>>>> GetAll(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _customerRepository.GetAllAsync(organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<Customer>>.Ok(result, "Customers retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var customer = new Customer
        {
            OrganizationId = organizationId,
            FullName = request.FullName,
            Phone = request.Phone,
            Email = request.Email,
            FacebookId = request.FacebookId,
            InstagramId = request.InstagramId,
            WhatsAppNumber = request.WhatsAppNumber
        };

        var id = await _customerRepository.CreateAsync(customer, cancellationToken);
        return Ok(ApiResponse<Guid>.Ok(id, "Customer created successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<Customer>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(id, GetOrganizationId(), cancellationToken);
        return customer is null ? NotFound(ApiResponse<Customer>.Fail("Customer not found.")) : Ok(ApiResponse<Customer>.Ok(customer));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(id, GetOrganizationId(), cancellationToken);
        if (customer is null) return NotFound(ApiResponse.Fail("Customer not found."));
        customer.FullName = request.FullName;
        customer.Phone = request.Phone;
        customer.Email = request.Email;
        customer.FacebookId = request.FacebookId;
        customer.InstagramId = request.InstagramId;
        customer.WhatsAppNumber = request.WhatsAppNumber;
        await _customerRepository.UpdateAsync(customer, cancellationToken);
        return Ok(ApiResponse.Ok("Customer updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireOrganizationAdmin")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _customerRepository.DeleteAsync(id, GetOrganizationId(), cancellationToken);
        return Ok(ApiResponse.Ok("Customer deleted successfully."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
