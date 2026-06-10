using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SadcOms.Application.Abstractions;
using SadcOms.Contracts;
using SadcOms.Infrastructure.Messaging;
using SadcOms.Infrastructure.Persistence;
using RabbitMQ.Client;

namespace SadcOms.IntegrationTests;

/// <summary>
/// Spins up the real API pipeline in-process with the external dependencies swapped out:
/// an in-memory database, a no-op event publisher and a stub RabbitMQ connection. The outbox
/// dispatcher hosted service is removed so no broker connection is attempted. Authentication
/// still runs for real against the symmetric dev key, so tests exercise the auth path too.
///
/// Configuration is supplied via environment variables in the constructor rather than
/// ConfigureAppConfiguration, because Program.cs reads the JWT/connection settings at
/// build time — before WebApplicationFactory's configuration callbacks run. Environment
/// variables are picked up by WebApplication.CreateBuilder's default providers, so they are
/// visible to that early read.
/// </summary>
public sealed class SadcOmsApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"it-{Guid.NewGuid()}";

    public SadcOmsApiFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("ConnectionStrings__SqlServer", "Server=unused;Database=unused;TrustServerCertificate=True");
        Environment.SetEnvironmentVariable("Jwt__Authority", "");
        Environment.SetEnvironmentVariable("Jwt__Audience", "sadc-oms-api");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "sadc-oms-dev");
        Environment.SetEnvironmentVariable("Jwt__DevSigningKey", "integration-test-symmetric-signing-key-min-32-bytes!!");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Replace SQL Server with the in-memory provider.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            // Drop the outbox dispatcher so the test host never reaches for RabbitMQ.
            services.RemoveAll<IHostedService>();

            // Stub messaging.
            services.RemoveAll<IEventPublisher>();
            services.AddSingleton<IEventPublisher, NoOpEventPublisher>();
            services.RemoveAll<IRabbitMqConnection>();
            services.AddSingleton<IRabbitMqConnection, StubRabbitMqConnection>();
        });
    }

    private sealed class NoOpEventPublisher : IEventPublisher
    {
        public Task PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubRabbitMqConnection : IRabbitMqConnection
    {
        public IModel CreateChannel() => throw new NotSupportedException("RabbitMQ is not used in integration tests.");
    }
}
