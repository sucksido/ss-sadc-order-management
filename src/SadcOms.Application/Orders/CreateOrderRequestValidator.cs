using FluentValidation;

namespace SadcOms.Application.Orders;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();

        RuleFor(x => x.CurrencyCode)
            .NotEmpty()
            .Length(3)
            .WithMessage("CurrencyCode must be a 3-letter ISO 4217 code.");

        RuleFor(x => x.Lines)
            .NotEmpty()
            .WithMessage("An order must contain at least one line item.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductSku).NotEmpty().MaximumLength(64);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}
