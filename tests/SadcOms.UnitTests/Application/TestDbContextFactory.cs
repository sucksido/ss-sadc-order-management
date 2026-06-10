using Microsoft.EntityFrameworkCore;
using SadcOms.Infrastructure.Persistence;

namespace SadcOms.UnitTests.Application;

/// <summary>
/// Builds an isolated in-memory <see cref="AppDbContext"/> per test. The EF in-memory provider
/// is sufficient for exercising service orchestration (it does not need relational features
/// here); endpoint-level behaviour is covered separately by the integration tests.
/// </summary>
internal static class TestDbContextFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"sadc-tests-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;

        return new AppDbContext(options);
    }
}
