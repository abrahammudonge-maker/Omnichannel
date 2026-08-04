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

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
