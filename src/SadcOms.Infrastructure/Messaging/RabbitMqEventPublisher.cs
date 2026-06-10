using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using SadcOms.Application.Abstractions;
using SadcOms.Contracts;

namespace SadcOms.Infrastructure.Messaging;

/// <summary>
/// Publishes integration events to the topic exchange with persistent delivery and publisher
/// confirms, so a successful return genuinely means the broker has the message on disk.
/// </summary>
public sealed class RabbitMqEventPublisher(IRabbitMqConnection connection, IOptions<RabbitMqOptions> options)
    : IEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RabbitMqOptions _options = options.Value;

    public Task PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        using var channel = connection.CreateChannel();
        channel.ConfirmSelect(); // enable publisher confirms

        var routingKey = @event switch
        {
            OrderCreatedIntegrationEvent => OrderCreatedIntegrationEvent.RoutingKey,
            _ => @event.GetType().Name
        };

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event, @event.GetType(), JsonOptions));

        var props = channel.CreateBasicProperties();
        props.ContentType = "application/json";
        props.DeliveryMode = 2; // persistent
        props.MessageId = @event.EventId.ToString();
        props.Type = @event.GetType().Name;
        props.CorrelationId = @event.CorrelationId;
        props.Timestamp = new AmqpTimestamp(@event.OccurredAt.ToUnixTimeSeconds());

        channel.BasicPublish(_options.Exchange, routingKey, mandatory: true, basicProperties: props, body: body);

        // Block until the broker acks the publish; throws on nack/timeout.
        channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
        return Task.CompletedTask;
    }
}
