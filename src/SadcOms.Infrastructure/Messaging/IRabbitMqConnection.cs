using RabbitMQ.Client;

namespace SadcOms.Infrastructure.Messaging;

/// <summary>Provides ready-to-use channels over a shared, topology-declared connection.</summary>
public interface IRabbitMqConnection
{
    IModel CreateChannel();
}
