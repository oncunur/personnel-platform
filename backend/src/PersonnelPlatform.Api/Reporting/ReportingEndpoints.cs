using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Application.Reporting;

namespace PersonnelPlatform.Api.Reporting;

public static class ReportingEndpoints
{
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/reports").WithTags("Reporting").RequireAuthorization();
        group.MapGet("/projects/{projectId:guid}/360", Project360Async).RequirePermission(ReportingPermissions.View);
        group.MapGet("/management", ManagementAsync).RequirePermission(ReportingPermissions.View);
        group.MapPost("/exports", CreateExportAsync).RequirePermission(ReportingPermissions.Export);
        group.MapGet("/exports", ListExportsAsync).RequirePermission(ReportingPermissions.Export);
        group.MapGet("/exports/{exportJobId:guid}/file", DownloadExportAsync).RequirePermission(ReportingPermissions.Export);
        return endpoints;
    }

    private static async Task<IResult> Project360Async(Guid projectId, ClaimsPrincipal p, ReportingService service, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        return ToResult(await service.GetProject360Async(userId, projectId, DateValue(c.Request.Query, "from"), DateValue(c.Request.Query, "to"), ct), c);
    }

    private static async Task<IResult> ManagementAsync(ClaimsPrincipal p, ReportingService service, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var companyId = GuidValue(c.Request.Query, "companyId");
        if (companyId is null) return Error(c, StatusCodes.Status400BadRequest, "COMPANY_REQUIRED", "Şirket seçimi zorunludur.");
        return ToResult(await service.ListManagementAsync(userId, companyId.Value, DateValue(c.Request.Query, "from"), DateValue(c.Request.Query, "to"), ct), c);
    }

    private static async Task<IResult> CreateExportAsync(CreateReportExportRequest request, ClaimsPrincipal p, ReportingService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var result = await service.CreateExportAsync(userId, request, ct);
        await AuditAsync(audit, logs, p, c, "REPORT_EXPORT_QUEUED", result.Succeeded, result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, c, StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListExportsAsync(ClaimsPrincipal p, ReportingService service, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        return ToResult(await service.ListExportsAsync(userId, GuidValue(c.Request.Query, "companyId"), IntValue(c.Request.Query, "take", 100), ct), c);
    }

    private static async Task<IResult> DownloadExportAsync(Guid exportJobId, ClaimsPrincipal p, ReportingService service, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var result = await service.DownloadExportAsync(userId, exportJobId, ct);
        if (!result.Succeeded || result.Value is null)
        {
            var code = result.ErrorCode ?? "REPORT_EXPORT_DOWNLOAD_FAILED";
            var status = code == "SCOPE_DENIED" ? StatusCodes.Status403Forbidden : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound : StatusCodes.Status409Conflict;
            return Error(c, status, code, result.ErrorMessage ?? "Export indirilemedi.");
        }
        return Results.Stream(result.Value.Content, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: false);
    }

    private static IResult ToResult<T>(ReportingResult<T> result, HttpContext c, int success = StatusCodes.Status200OK) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Json(result.Value, statusCode: success);
        var code = result.ErrorCode ?? "REPORT_OPERATION_FAILED";
        var status = code == "SCOPE_DENIED" || code.EndsWith("_DENIED", StringComparison.Ordinal) ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code == "RECORD_MODIFIED_BY_ANOTHER_USER" ? StatusCodes.Status409Conflict
            : StatusCodes.Status422UnprocessableEntity;
        return Error(c, status, code, result.ErrorMessage ?? "Rapor işlemi tamamlanamadı.");
    }

    private static async Task AuditAsync(AuditService audit, ILoggerFactory logs, ClaimsPrincipal p, HttpContext c, string eventType, bool succeeded, Guid? entityId, string? errorCode, string? message, CancellationToken ct)
    {
        try { await audit.WriteAsync(new AuditEvent(AuditCategories.Administration, eventType, succeeded, succeeded ? AuditSeverities.Info : AuditSeverities.Warning, Actor(p, out var actor) ? actor : null, p.FindFirstValue("unique_name"), c.Connection.RemoteIpAddress?.ToString(), c.Request.Headers["User-Agent"].ToString(), c.TraceIdentifier, "REPORT_EXPORT", entityId?.ToString(), errorCode, message), ct); }
        catch (Exception ex) { logs.CreateLogger("ReportingAudit").LogError(ex, "Reporting audit write failed for {EventType}.", eventType); }
    }

    private static Guid? GuidValue(IQueryCollection q, string key) => Guid.TryParse(q[key].ToString(), out var value) ? value : null;
    private static DateOnly? DateValue(IQueryCollection q, string key) => DateOnly.TryParse(q[key].ToString(), out var value) ? value : null;
    private static int IntValue(IQueryCollection q, string key, int fallback) => int.TryParse(q[key].ToString(), out var value) ? value : fallback;
    private static bool Actor(ClaimsPrincipal p, out Guid userId) => Guid.TryParse(p.FindFirstValue("sub"), out userId);
    private static IResult Unauthorized(HttpContext c) => Error(c, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");
    private static IResult Error(HttpContext c, int status, string code, string message) => Results.Json(ApiErrorResponse.Create(code, message, c.TraceIdentifier), statusCode: status);
}
