using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SadcOms.Application.Customers;
using SadcOms.Application.Orders;
using SadcOms.Application.Reports;

namespace SadcOms.Application;

public static class DependencyInjection
{
    /// <summary>Registers application use-case services and FluentValidation validators.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IZarReportService, ZarReportService>();

        services.AddValidatorsFromAssemblyContaining<CreateCustomerRequestValidator>();

        return services;
    }
}
