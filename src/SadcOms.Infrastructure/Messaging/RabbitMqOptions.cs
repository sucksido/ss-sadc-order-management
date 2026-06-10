namespace SadcOms.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";

    /// <summary>Topic exchange that order events are published to.</summary>
    public string Exchange { get; set; } = "sadc.orders";

    /// <summary>Queue the worker binds for OrderCreated events.</summary>
    public string OrderCreatedQueue { get; set; } = "order-created";

    /// <summary>Dead-letter exchange/queue for messages that exhaust retries.</summary>
    public string DeadLetterExchange { get; set; } = "sadc.orders.dlx";
    public string DeadLetterQueue { get; set; } = "order-created.dead-letter";
}
