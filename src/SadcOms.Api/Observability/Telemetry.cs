using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SadcOms.Api.Observability;

/// <summary>
/// Central names for the app's custom tracing and metrics instruments. Registered with
/// OpenTelemetry in Program.cs so they are exported alongside the framework instrumentation.
/// </summary>
public static class Telemetry
{
    public const string ServiceName = "SadcOms.Api";

    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);

    public static readonly Counter<long> OrdersCreated =
        Meter.CreateCounter<long>("sadc_orders_created_total", description: "Number of orders created.");

    public static readonly Counter<long> OrderStatusChanged =
        Meter.CreateCounter<long>("sadc_order_status_changed_total", description: "Number of order status transitions.");
}
