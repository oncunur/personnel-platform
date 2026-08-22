namespace PersonnelPlatform.Api.Contracts;

public sealed record ApiErrorResponse(bool Success, ApiError Error)
{
    public static ApiErrorResponse Create(string code, string message, string traceId) =>
        new(false, new ApiError(code, message, traceId));
}

public sealed record ApiError(string Code, string Message, string TraceId);
