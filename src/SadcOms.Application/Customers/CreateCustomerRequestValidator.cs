using FluentValidation;
using SadcOms.Domain.Regional;

namespace SadcOms.Application.Customers;

public sealed class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320); // RFC 5321 maximum

        RuleFor(x => x.CountryCode)
            .NotEmpty()
            .Length(2)
            .Must(SadcRegion.IsSadcCountry)
            .WithMessage("CountryCode must be a supported SADC ISO 3166-1 alpha-2 code.");
    }
}
