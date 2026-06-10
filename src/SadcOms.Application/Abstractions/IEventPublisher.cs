using SadcOms.Contracts;

namespace SadcOms.Application.Abstractions;

/// <summary>Publishes integration events to the message broker. Implemented over RabbitMQ.</summary>
public interface IEventPublisher
{
    Task PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default);
}
