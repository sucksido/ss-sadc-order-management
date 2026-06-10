using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SadcOms.Application.Common;
using SadcOms.Application.Customers;

namespace SadcOms.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class CustomersController(ICustomerService customers) : ControllerBase
{
    /// <summary>Creates a customer. CountryCode must be a supported SADC ISO 3166-1 alpha-2 code.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerDto>> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        var created = await customers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> GetById(Guid id, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(id, ct);
        return customer is null ? NotFound() : Ok(customer);
    }

    /// <summary>Lists customers with optional name/email search and pagination (pageSize capped at 100).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CustomerDto>>> Search(
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var result = await customers.SearchAsync(search, new PageRequest(page, pageSize), ct);
        return Ok(result);
    }
}
