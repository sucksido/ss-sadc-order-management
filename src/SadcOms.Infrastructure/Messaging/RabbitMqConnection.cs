using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace SadcOms.Infrastructure.Messaging;

/// <summary>
/// Owns a single long-lived RabbitMQ <see cref="IConnection"/> (the expensive resource) and
/// declares the exchange/queue topology once. Channels are cheap and created per-use by the
/// publisher/consumer. Connecting is retried with backoff so the service tolerates the broker
/// not being ready yet at start-up (common in container orchestration).
/// </summary>
public sealed class RabbitMqConnection : IRabbitMqConnection, IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqConnection> _logger;
    private readonly ResiliencePipeline _connectRetry;
    private readonly object _gate = new();
    private IConnection? _connection;
    private bool _topologyDeclared;

    public RabbitMqConnection(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConnection> logger)
    {
        _options = options.Value;
        _logger = logger;

        _connectRetry = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<BrokerUnreachableException>()
                    .Handle<SocketException>(),
                MaxRetryAttempts = 8,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    _logger.LogWarning(args.Outcome.Exception,
                        "RabbitMQ connection attempt {Attempt} failed; retrying in {Delay}.",
                        args.AttemptNumber + 1, args.RetryDelay);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public IModel CreateChannel()
    {
        EnsureConnected();
        var channel = _connection!.CreateModel();
        DeclareTopology(channel);
        return channel;
    }

    private void EnsureConnected()
    {
        if (_connection is { IsOpen: true })
        {
            return;
        }

        lock (_gate)
        {
            if (_connection is { IsOpen: true })
            {
                return;
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                DispatchConsumersAsync = true,
                AutomaticRecoveryEnabled = true
            };

            _connection = _connectRetry.Execute(() => factory.CreateConnection("sadc-oms"));
            _logger.LogInformation("Connected to RabbitMQ at {Host}:{Port}.", _options.HostName, _options.Port);
        }
    }

    private void DeclareTopology(IModel channel)
    {
        if (_topologyDeclared)
        {
            return;
        }

        lock (_gate)
        {
            if (_topologyDeclared)
            {
                return;
            }

            // Dead-letter path first so the main queue can reference it.
            channel.ExchangeDeclare(_options.DeadLetterExchange, ExchangeType.Fanout, durable: true);
            channel.QueueDeclare(_options.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false);
            channel.QueueBind(_options.DeadLetterQueue, _options.DeadLetterExchange, routingKey: string.Empty);

            channel.ExchangeDeclare(_options.Exchange, ExchangeType.Topic, durable: true);
            channel.QueueDeclare(
                _options.OrderCreatedQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object> { ["x-dead-letter-exchange"] = _options.DeadLetterExchange });
            channel.QueueBind(_options.OrderCreatedQueue, _options.Exchange, routingKey: "order.*");

            _topologyDeclared = true;
        }
    }

    public void Dispose() => _connection?.Dispose();
}
