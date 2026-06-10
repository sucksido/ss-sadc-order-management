using Microsoft.EntityFrameworkCore;
using SadcOms.Application.Abstractions;
using SadcOms.Application.Common;
using SadcOms.Domain.Customers;

namespace SadcOms.Application.Customers;

public sealed class CustomerService(IAppDbContext db, TimeProvider clock) : ICustomerService
{
    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        // Domain factory enforces invariants (valid email, supported SADC country).
        var customer = Customer.Create(request.Name, request.Email, request.CountryCode, clock);

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);

        return CustomerDto.FromEntity(customer);
    }

    public async Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return customer is null ? null : CustomerDto.FromEntity(customer);
    }

    public async Task<PagedResult<CustomerDto>> SearchAsync(string? search, PageRequest page, CancellationToken cancellationToken = default)
    {
        var query = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            // Translated to SQL LIKE; collation makes this case-insensitive on SQL Server.
            query = query.Where(c => EF.Functions.Like(c.Name, $"%{term}%")
                                  || EF.Functions.Like(c.Email, $"%{term}%"));
        }

        var total = await query.LongCountAsync(cancellationToken);

        var entities = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenBy(c => c.Id) // stable tie-breaker so paging is deterministic
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        // Map after materialisation: the DTO factory isn't translatable to SQL.
        var items = entities.Select(CustomerDto.FromEntity).ToList();
        return new PagedResult<CustomerDto>(items, page.Page, page.PageSize, total);
    }
}
