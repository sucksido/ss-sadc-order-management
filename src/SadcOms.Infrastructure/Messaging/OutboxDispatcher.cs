using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SadcOms.Application.Abstractions;
using SadcOms.Contracts;
using SadcOms.Infrastructure.Persistence;

namespace SadcOms.Infrastructure.Messaging;

/// <summary>
/// Background dispatcher for the transactional outbox. On an interval it claims a batch of
/// unprocessed rows oldest-first, publishes each to RabbitMQ, and marks it processed. A
/// failed publish is recorded (attempt count + error) and retried on the next pass; the row
/// stays in the table so nothing is lost. This is the "reliable publishing" half of the
/// outbox pattern — producers only ever write to the database.
/// </summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IEventPublisher publisher,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox dispatcher started (interval {Interval}, batch {Batch}).",
            _options.PollInterval, _options.BatchSize);

        using var timer = new PeriodicTimer(_options.PollInterval);
        do
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Never let a transient failure kill the loop.
                logger.LogError(ex, "Outbox dispatch pass failed; will retry next interval.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var batch = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.Attempts < _options.MaxAttempts)
            .OrderBy(m => m.OccurredAt)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        if (batch.Count == 0)
        {
            return;
        }

        foreach (var message in batch)
        {
            try
            {
                var @event = Deserialize(message.Type, message.Payload);
                if (@event is null)
                {
                    message.RecordFailure($"Unknown outbox message type '{message.Type}'.");
                    continue;
                }

                await publisher.PublishAsync(@event, ct);
                message.MarkProcessed(DateTimeOffset.UtcNow);
            }
            catch (Exception ex)
            {
                message.RecordFailure(ex.Message);
                logger.LogWarning(ex, "Failed to publish outbox message {MessageId} (attempt {Attempts}).",
                    message.Id, message.Attempts);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static IntegrationEvent? Deserialize(string type, string payload) => type switch
    {
        nameof(OrderCreatedIntegrationEvent) =>
            JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(payload, JsonOptions),
        _ => null
    };
}
