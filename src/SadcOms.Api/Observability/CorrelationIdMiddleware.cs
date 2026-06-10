using System.Diagnostics;
using Serilog.Context;

namespace SadcOms.Api.Observability;

/// <summary>
/// Ensures every request has a correlation id. It is taken from the inbound header if present
/// (so a caller can stitch a whole flow together), otherwise the current trace id is used.
/// The id is pushed into the Serilog log context and echoed back on the response, and flows
/// onward to RabbitMQ via the integration event.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationId.HeaderName, out var header)
                            && !string.IsNullOrWhiteSpace(header)
            ? header.ToString()
            : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("n");

        context.Items[CorrelationId.ItemKey] = correlationId;
        Activity.Current?.SetTag("correlation_id", correlationId);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationId.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
