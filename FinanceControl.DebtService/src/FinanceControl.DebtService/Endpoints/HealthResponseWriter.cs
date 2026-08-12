using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FinanceControl.DebtService.Endpoints;

internal static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var response = new HealthResponse(report.Status.ToString(), "debt-service");
        return context.Response.WriteAsync(JsonSerializer.Serialize(response, SerializerOptions));
    }

    private sealed record HealthResponse(string Status, string Service);
}
