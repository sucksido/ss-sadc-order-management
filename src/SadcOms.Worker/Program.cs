using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SadcOms.Infrastructure.Messaging;
using SadcOms.Worker;
using Serilog;
using Serilog.Events;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((_, loggerConfig) => loggerConfig
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

// The worker only needs the RabbitMQ connection (not the database), so it wires up messaging
// directly rather than pulling in the full infrastructure registration.
builder.Services.AddOptions<RabbitMqOptions>().BindConfiguration(RabbitMqOptions.SectionName);
builder.Services.AddSingleton<IRabbitMqConnection, RabbitMqConnection>();
builder.Services.AddHostedService<OrderCreatedConsumer>();

var host = builder.Build();
host.Run();
