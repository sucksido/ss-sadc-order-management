using SadcOms.Domain.Customers;

namespace SadcOms.Application.Customers;

public sealed record CreateCustomerRequest(string Name, string Email, string CountryCode);

public sealed record CustomerDto(Guid Id, string Name, string Email, string CountryCode, DateTimeOffset CreatedAt)
{
    public static CustomerDto FromEntity(Customer c) => new(c.Id, c.Name, c.Email, c.CountryCode, c.CreatedAt);
}
