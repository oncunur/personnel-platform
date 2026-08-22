using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PersonnelPlatform.Api;

public static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        return context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 2)
            }),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            traceId = context.TraceIdentifier
        });
    }
}
