using System.Diagnostics;

namespace FinanceControl.DebtService.Observability;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemName = "FinanceControl.CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request);
        context.Items[ItemName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation(
            "HTTP request started {RequestMethod} {RequestPath}",
            context.Request.Method,
            context.Request.Path);

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "HTTP request completed {RequestMethod} {RequestPath} with {StatusCode} in {ElapsedMilliseconds} ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    public static string? GetCorrelationId(HttpContext context) =>
        context.Items.TryGetValue(ItemName, out var value) ? value as string : null;

    private static string ResolveCorrelationId(HttpRequest request)
    {
        var incomingValue = request.Headers[HeaderName];
        return incomingValue.Count == 1 && Guid.TryParse(incomingValue[0], out var correlationId)
            ? correlationId.ToString("D")
            : Guid.NewGuid().ToString("D");
    }
}
