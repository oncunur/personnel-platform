using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Application.Migration;

namespace PersonnelPlatform.Api.Migration;

public static class MigrationEndpoints
{
    public static IEndpointRouteBuilder MapMigrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/migrations").WithTags("Migration").RequireAuthorization();
        group.MapGet("/runs", ListRunsAsync).RequirePermission(MigrationPermissions.View);
        group.MapGet("/runs/{runId:guid}", GetRunAsync).RequirePermission(MigrationPermissions.View);
        group.MapGet("/runs/{runId:guid}/rows", ListRowsAsync).RequirePermission(MigrationPermissions.View);
        group.MapPost("/runs", CreateRunAsync).RequirePermission(MigrationPermissions.Manage);
        group.MapPost("/runs/{runId:guid}/stage", StageRowsAsync).RequirePermission(MigrationPermissions.Manage);
        group.MapPost("/runs/{runId:guid}/validate", ValidateRunAsync).RequirePermission(MigrationPermissions.Manage);
        group.MapPost("/runs/{runId:guid}/reconcile", ReconcileRunAsync).RequirePermission(MigrationPermissions.Reconcile);
        return endpoints;
    }

    private static async Task<IResult> ListRunsAsync(ClaimsPrincipal p, MigrationService service, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var companyId = Guid.TryParse(c.Request.Query["companyId"], out var company) ? company : (Guid?)null;
        var take = int.TryParse(c.Request.Query["take"], out var parsed) ? parsed : 100;
        return ToResult(await service.ListRunsAsync(userId, companyId, take, ct), c);
    }

    private static async Task<IResult> GetRunAsync(Guid runId, ClaimsPrincipal p, MigrationService service, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        return ToResult(await service.GetRunAsync(userId, runId, ct), c);
    }

    private static async Task<IResult> ListRowsAsync(Guid runId, ClaimsPrincipal p, MigrationService service, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var status = c.Request.Query["status"].ToString();
        var take = int.TryParse(c.Request.Query["take"], out var parsed) ? parsed : 500;
        return ToResult(await service.ListRowsAsync(userId, runId, status, take, ct), c);
    }

    private static async Task<IResult> CreateRunAsync(CreateMigrationRunRequest request, ClaimsPrincipal p, MigrationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var result = await service.CreateRunAsync(userId, request, ct);
        await AuditAsync(audit, logs, p, c, "MIGRATION_RUN_CREATED", result.Succeeded, result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, c, StatusCodes.Status201Created);
    }

    private static async Task<IResult> StageRowsAsync(Guid runId, StageMigrationRowsRequest request, ClaimsPrincipal p, MigrationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var result = await service.StageRowsAsync(userId, runId, request, ct);
        await AuditAsync(audit, logs, p, c, "MIGRATION_ROWS_STAGED", result.Succeeded, runId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, c);
    }

    private static async Task<IResult> ValidateRunAsync(Guid runId, ValidateMigrationRunRequest request, ClaimsPrincipal p, MigrationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var result = await service.ValidateRunAsync(userId, runId, request, ct);
        await AuditAsync(audit, logs, p, c, "MIGRATION_RUN_VALIDATED", result.Succeeded, runId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, c);
    }

    private static async Task<IResult> ReconcileRunAsync(Guid runId, ReconcileMigrationRunRequest request, ClaimsPrincipal p, MigrationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var result = await service.ReconcileRunAsync(userId, runId, request, ct);
        await AuditAsync(audit, logs, p, c, "MIGRATION_RUN_RECONCILED", result.Succeeded, runId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, c);
    }

    private static IResult ToResult<T>(MigrationResult<T> result, HttpContext c, int success = StatusCodes.Status200OK) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Json(result.Value, statusCode: success);
        var code = result.ErrorCode ?? "MIGRATION_OPERATION_FAILED";
        var status = code is "PERMISSION_DENIED" or "SCOPE_DENIED" ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code is "RECORD_MODIFIED_BY_ANOTHER_USER" or "MIGRATION_RECONCILIATION_EXISTS" ? StatusCodes.Status409Conflict
            : code.Contains("INVALID", StringComparison.Ordinal) || code.Contains("DUPLICATE", StringComparison.Ordinal) || code.Contains("REQUIRED", StringComparison.Ordinal) ? StatusCodes.Status400BadRequest
            : StatusCodes.Status422UnprocessableEntity;
        return Error(c, status, code, result.ErrorMessage ?? "Migration işlemi tamamlanamadı.");
    }

    private static async Task AuditAsync(AuditService audit, ILoggerFactory logs, ClaimsPrincipal p, HttpContext c, string eventType, bool succeeded, Guid? runId, string? errorCode, string? message, CancellationToken ct)
    {
        try
        {
            await audit.WriteAsync(new AuditEvent(AuditCategories.Administration, eventType, succeeded, succeeded ? AuditSeverities.Info : AuditSeverities.Warning,
                Actor(p, out var actor) ? actor : null, p.FindFirstValue("unique_name"), c.Connection.RemoteIpAddress?.ToString(), c.Request.Headers["User-Agent"].ToString(), c.TraceIdentifier,
                "MIGRATION_RUN", runId?.ToString(), errorCode, message), ct);
        }
        catch (Exception ex) { logs.CreateLogger("MigrationAudit").LogError(ex, "Migration audit write failed for {EventType}.", eventType); }
    }

    private static bool Actor(ClaimsPrincipal p, out Guid userId) => Guid.TryParse(p.FindFirstValue("sub"), out userId);
    private static IResult Unauthorized(HttpContext c) => Error(c, 401, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");
    private static IResult Error(HttpContext c, int status, string code, string message) => Results.Json(ApiErrorResponse.Create(code, message, c.TraceIdentifier), statusCode: status);
}