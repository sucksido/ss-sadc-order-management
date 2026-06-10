using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SadcOms.Contracts;
using SadcOms.Infrastructure.Messaging;

namespace SadcOms.Worker;

/// <summary>
/// Consumes OrderCreated events and simulates downstream fulfilment allocation.
///
/// Reliability model:
///   - Manual acknowledgements: a message is only acked once processing succeeds.
///   - In-process retry with exponential backoff (Polly) absorbs transient failures.
///   - On exhausted retries the message is rejected without requeue, so the broker routes it
///     to the configured dead-letter queue for later inspection rather than hot-looping.
///   - EventId de-duplication guards against re-processing on redelivery (at-least-once
///     delivery means duplicates are possible). A durable store (DB/Redis) would replace the
///     in-memory set in production; the set is sufficient for this assessment.
/// </summary>
public sealed class OrderCreatedConsumer(
    IRabbitMqConnection connection,
    IOptions<RabbitMqOptions> options,
    ILogger<OrderCreatedConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RabbitMqOptions _options = options.Value;
    private readonly ConcurrentDictionary<Guid, byte> _processedEventIds = new();

    private readonly ResiliencePipeline _processingRetry = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(500),
            BackoffType = DelayBackoffType.Exponential
        })
        .Build();

    private IModel? _channel;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = connection.CreateChannel();

        // Limit unacked messages in flight so one consumer can't be overwhelmed (backpressure).
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnReceivedAsync;

        _channel.BasicConsume(_options.OrderCreatedQueue, autoAck: false, consumer);
        logger.LogInformation("Consuming '{Queue}' for OrderCreated events.", _options.OrderCreatedQueue);

        return Task.CompletedTask;
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs args)
    {
        var channel = _channel!;
        OrderCreatedIntegrationEvent? @event = null;

        try
        {
            var json = Encoding.UTF8.GetString(args.Body.Span);
            @event = JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(json, JsonOptions);

            if (@event is null)
            {
                logger.LogWarning("Received an OrderCreated message that could not be deserialised; dead-lettering.");
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            if (!_processedEventIds.TryAdd(@event.EventId, 0))
            {
                logger.LogInformation("Duplicate event {EventId} ignored.", @event.EventId);
                channel.BasicAck(args.DeliveryTag, multiple: false);
                return;
            }

            await _processingRetry.ExecuteAsync(async _ => await SimulateFulfillmentAsync(@event), CancellationToken.None);

            channel.BasicAck(args.DeliveryTag, multiple: false);
            logger.LogInformation(
                "Fulfilment simulated for order {OrderId} (total {Total} {Currency}). CorrelationId={CorrelationId}",
                @event.OrderId, @event.TotalAmount, @event.CurrencyCode, @event.CorrelationId);
        }
        catch (Exception ex)
        {
            // Allow a future redelivery to retry by removing the dedupe marker.
            if (@event is not null)
            {
                _processedEventIds.TryRemove(@event.EventId, out _);
            }

            logger.LogError(ex, "Failed to process OrderCreated message after retries; dead-lettering.");
            channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
        }
    }

    private static async Task SimulateFulfillmentAsync(OrderCreatedIntegrationEvent @event)
    {
        // Stand-in for real work: reserve stock, notify a warehouse, etc.
        await Task.Delay(TimeSpan.FromMilliseconds(150));
        _ = @event;
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        base.Dispose();
    }
}
