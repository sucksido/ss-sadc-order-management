using SadcOms.Domain.Common;
using SadcOms.Domain.Regional;

namespace SadcOms.Domain.Customers;

/// <summary>
/// A customer placing orders. The country code is validated against the SADC list so we
/// never persist a customer we could not later transact with.
/// </summary>
public sealed class Customer
{
    // Parameterless ctor for EF Core materialisation.
    private Customer()
    {
        Name = null!;
        Email = null!;
        CountryCode = null!;
    }

    private Customer(Guid id, string name, string email, string countryCode, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Email = email;
        CountryCode = countryCode;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Email { get; private set; }
    public string CountryCode { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Customer Create(string name, string email, string countryCode, TimeProvider? clock = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Customer name is required.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new DomainException("A valid customer email is required.");
        }

        if (!SadcRegion.IsSadcCountry(countryCode))
        {
            throw new DomainException($"'{countryCode}' is not a supported SADC country code.");
        }

        var now = (clock ?? TimeProvider.System).GetUtcNow();
        return new Customer(Guid.NewGuid(), name.Trim(), email.Trim(), countryCode.Trim().ToUpperInvariant(), now);
    }
}
