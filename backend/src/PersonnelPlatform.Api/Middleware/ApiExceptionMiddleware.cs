using System.Net.Mime;
using PersonnelPlatform.Api.Contracts;

namespace PersonnelPlatform.Api.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request {TraceId} was cancelled by the client.", context.TraceIdentifier);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception. TraceId={TraceId}", context.TraceIdentifier);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = MediaTypeNames.Application.Json;

            var response = ApiErrorResponse.Create(
                code: "UNEXPECTED_ERROR",
                message: "Beklenmeyen bir sistem hatası oluştu.",
                traceId: context.TraceIdentifier);

            await context.Response.WriteAsJsonAsync(response, context.RequestAborted);
        }
    }
}
