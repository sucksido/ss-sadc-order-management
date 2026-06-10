using Microsoft.AspNetCore.Mvc;
using SadcOms.Api.Observability;
using SadcOms.Application.Common;
using SadcOms.Domain.Common;

namespace SadcOms.Api.Middleware;

/// <summary>
/// Translates exceptions into RFC 7807 ProblemDetails responses. Domain/validation errors
/// become 4xx with a useful message; everything else is a 500 with the detail hidden from
/// the client but logged with the correlation id for diagnosis.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        var correlationId = context.Items[CorrelationId.ItemKey]?.ToString();

        var (status, title) = ex switch
        {
            InvalidStatusTransitionException => (StatusCodes.Status409Conflict, "Invalid status transition"),
            DomainException => (StatusCodes.Status400BadRequest, "Validation error"),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(ex, "Unhandled exception. CorrelationId={CorrelationId}", correlationId);
        }
        else
        {
            logger.LogWarning("Handled {Status} ({Title}): {Message}", status, title, ex.Message);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == StatusCodes.Status500InternalServerError ? "Please contact support with the correlation id." : ex.Message,
            Type = $"https://httpstatuses.com/{status}"
        };
        problem.Extensions["correlationId"] = correlationId;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
