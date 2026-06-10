using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SadcOms.Application.Abstractions;
using SadcOms.Infrastructure.Fx;
using SadcOms.Infrastructure.Messaging;
using SadcOms.Infrastructure.Persistence;

namespace SadcOms.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence, FX and messaging building blocks. Hosted services (the outbox
    /// dispatcher, the consumer) are added explicitly by the host that owns them so the API
    /// and Worker can share this registration without both running every background loop.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SqlServer")
            ?? throw new InvalidOperationException("Connection string 'SqlServer' is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            }));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton(TimeProvider.System);

        // FX
        services.AddMemoryCache();
        services.AddOptions<FxOptions>().BindConfiguration(FxOptions.SectionName);
        services.AddSingleton<IFxRateProvider, MockFxRateProvider>();

        // Messaging
        services.AddOptions<RabbitMqOptions>().BindConfiguration(RabbitMqOptions.SectionName);
        services.AddOptions<OutboxOptions>().BindConfiguration(OutboxOptions.SectionName);
        services.AddSingleton<IRabbitMqConnection, RabbitMqConnection>();
        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

        return services;
    }

    /// <summary>Adds the outbox dispatcher hosted service. Called by the API (the producer side).</summary>
    public static IServiceCollection AddOutboxDispatcher(this IServiceCollection services)
    {
        services.AddHostedService<OutboxDispatcher>();
        return services;
    }
}
