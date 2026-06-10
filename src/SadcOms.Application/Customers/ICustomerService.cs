using SadcOms.Application.Common;

namespace SadcOms.Application.Customers;

public interface ICustomerService
{
    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);
    Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<CustomerDto>> SearchAsync(string? search, PageRequest page, CancellationToken cancellationToken = default);
}
