using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Application.Integration;
using PersonnelPlatform.Domain.Integration;

namespace PersonnelPlatform.Api.Integration;

public static class IntegrationEndpoints
{
    public static IEndpointRouteBuilder MapIntegrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/integrations").WithTags("Integrations").RequireAuthorization();
        group.MapGet("/systems", ListSystemsAsync).RequirePermission(IntegrationPermissions.SystemView);
        group.MapPost("/systems", CreateSystemAsync).RequirePermission(IntegrationPermissions.SystemManage);
        group.MapPut("/systems/{systemId:guid}", UpdateSystemAsync).RequirePermission(IntegrationPermissions.SystemManage);
        group.MapGet("/systems/{systemId:guid}/devices", ListDevicesAsync).RequirePermission(IntegrationPermissions.SystemView);
        group.MapPost("/devices", CreateDeviceAsync).RequirePermission(IntegrationPermissions.SystemManage);
        group.MapPut("/devices/{deviceId:guid}", UpdateDeviceAsync).RequirePermission(IntegrationPermissions.SystemManage);
        group.MapPost("/devices/{deviceId:guid}/rotate-key", RotateKeyAsync).RequirePermission(IntegrationPermissions.SystemManage);
        group.MapGet("/systems/{systemId:guid}/mappings", ListMappingsAsync).RequirePermission(IntegrationPermissions.MappingView);
        group.MapPost("/mappings", CreateMappingAsync).RequirePermission(IntegrationPermissions.MappingManage);
        group.MapPut("/mappings/{mappingId:guid}", UpdateMappingAsync).RequirePermission(IntegrationPermissions.MappingManage);
        group.MapGet("/queue", ListQueueAsync).RequirePermission(IntegrationPermissions.QueueView);
        group.MapGet("/queue/{stagingId:guid}/history", ListHistoryAsync).RequirePermission(IntegrationPermissions.QueueView);
        group.MapPost("/queue/{stagingId:guid}/reprocess", ReprocessAsync).RequirePermission(IntegrationPermissions.QueueReprocess);
        group.MapGet("/monitoring", MonitoringAsync).RequirePermission(IntegrationPermissions.MonitorView);
        return endpoints;
    }

    public static IEndpointRouteBuilder MapExternalIntegrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/integrations/v1").WithTags("External Integrations");
        group.MapPost("/attendance/events", AttendanceEventAsync);
        group.MapPost("/meals/events/batch", MealBatchAsync);
        return endpoints;
    }

    private static async Task<IResult> ListSystemsAsync(ClaimsPrincipal p, IntegrationService service, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); return ToResult(await service.ListSystemsAsync(u, GuidValue(c.Request.Query, "companyId"), ct), c); }

    private static async Task<IResult> CreateSystemAsync(CreateIntegrationSystemRequest request, ClaimsPrincipal p, IntegrationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.CreateSystemAsync(u, request, ct); await AuditAsync(audit, logs, p, c, "INTEGRATION_SYSTEM_CREATED", r.Succeeded, r.Value?.Id, r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c, StatusCodes.Status201Created); }

    private static async Task<IResult> UpdateSystemAsync(Guid systemId, UpdateIntegrationSystemRequest request, ClaimsPrincipal p, IntegrationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.UpdateSystemAsync(u, systemId, request, ct); await AuditAsync(audit, logs, p, c, "INTEGRATION_SYSTEM_UPDATED", r.Succeeded, systemId, r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c); }

    private static async Task<IResult> ListDevicesAsync(Guid systemId, ClaimsPrincipal p, IntegrationService service, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); return ToResult(await service.ListDevicesAsync(u, systemId, ct), c); }

    private static async Task<IResult> CreateDeviceAsync(CreateIntegrationDeviceRequest request, ClaimsPrincipal p, IntegrationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.CreateDeviceAsync(u, request, ct); await AuditAsync(audit, logs, p, c, "INTEGRATION_DEVICE_CREATED", r.Succeeded, r.Value?.Device.Id, r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c, StatusCodes.Status201Created); }

    private static async Task<IResult> UpdateDeviceAsync(Guid deviceId, UpdateIntegrationDeviceRequest request, ClaimsPrincipal p, IntegrationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.UpdateDeviceAsync(u, deviceId, request, ct); await AuditAsync(audit, logs, p, c, "INTEGRATION_DEVICE_UPDATED", r.Succeeded, deviceId, r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c); }

    private static async Task<IResult> RotateKeyAsync(Guid deviceId, RotateIntegrationDeviceCredentialRequest request, ClaimsPrincipal p, IntegrationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.RotateDeviceCredentialAsync(u, deviceId, request, ct); await AuditAsync(audit, logs, p, c, "INTEGRATION_DEVICE_KEY_ROTATED", r.Succeeded, deviceId, r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c); }

    private static async Task<IResult> ListMappingsAsync(Guid systemId, ClaimsPrincipal p, IntegrationService service, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); return ToResult(await service.ListMappingsAsync(u, systemId, Text(c.Request.Query, "entityType"), ct), c); }

    private static async Task<IResult> CreateMappingAsync(CreateIntegrationMappingRequest request, ClaimsPrincipal p, IntegrationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.CreateMappingAsync(u, request, ct); await AuditAsync(audit, logs, p, c, "INTEGRATION_MAPPING_CREATED", r.Succeeded, r.Value?.Id, r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c, StatusCodes.Status201Created); }

    private static async Task<IResult> UpdateMappingAsync(Guid mappingId, UpdateIntegrationMappingRequest request, ClaimsPrincipal p, IntegrationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.UpdateMappingAsync(u, mappingId, request, ct); await AuditAsync(audit, logs, p, c, "INTEGRATION_MAPPING_UPDATED", r.Succeeded, mappingId, r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c); }

    private static async Task<IResult> ListQueueAsync(ClaimsPrincipal p, IntegrationService service, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var u)) return Unauthorized(c); var q = c.Request.Query;
        return ToResult(await service.ListQueueAsync(u, new IntegrationQueueQuery(GuidValue(q, "companyId"), GuidValue(q, "systemId"), Text(q, "eventType"), Text(q, "status"), IntValue(q, "take", 200)), ct), c);
    }

    private static async Task<IResult> ListHistoryAsync(Guid stagingId, ClaimsPrincipal p, IntegrationService service, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); return ToResult(await service.ListHistoryAsync(u, stagingId, ct), c); }

    private static async Task<IResult> ReprocessAsync(Guid stagingId, ReprocessStagingRequest request, ClaimsPrincipal p, IntegrationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.ReprocessAsync(u, stagingId, request, ct); await AuditAsync(audit, logs, p, c, "INTEGRATION_STAGING_REQUEUED", r.Succeeded, stagingId, r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c); }

    private static async Task<IResult> MonitoringAsync(ClaimsPrincipal p, IntegrationService service, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var companyId = GuidValue(c.Request.Query, "companyId"); if (companyId is null) return Error(c, 400, "COMPANY_REQUIRED", "companyId zorunludur."); return ToResult(await service.GetMonitoringAsync(u, companyId.Value, ct), c); }

    private static async Task<IResult> AttendanceEventAsync(AttendanceIntegrationEventRequest request, IntegrationService service, HttpContext c, CancellationToken ct)
    {
        var auth = await service.AuthenticateDeviceAsync(DeviceHeaders(c), IntegrationSystemTypes.Pdks, ct);
        if (!auth.Succeeded || auth.Value is null) return Error(c, 401, auth.ErrorCode ?? "INTEGRATION_DEVICE_AUTH_FAILED", auth.ErrorMessage ?? "Cihaz doğrulanamadı.");
        var result = await service.StageAttendanceAsync(auth.Value, request, ct);
        return ExternalResult(result, c);
    }

    private static async Task<IResult> MealBatchAsync(MealIntegrationBatchRequest request, IntegrationService service, HttpContext c, CancellationToken ct)
    {
        var auth = await service.AuthenticateDeviceAsync(DeviceHeaders(c), IntegrationSystemTypes.Meal, ct);
        if (!auth.Succeeded || auth.Value is null) return Error(c, 401, auth.ErrorCode ?? "INTEGRATION_DEVICE_AUTH_FAILED", auth.ErrorMessage ?? "Cihaz doğrulanamadı.");
        var result = await service.StageMealBatchAsync(auth.Value, request, ct);
        return ExternalResult(result, c);
    }

    private static ExternalDeviceHeaders DeviceHeaders(HttpContext c) => new(c.Request.Headers["X-Company-Code"].ToString(), c.Request.Headers["X-Integration-System"].ToString(), c.Request.Headers["X-Device-Code"].ToString(), c.Request.Headers["X-Device-Key"].ToString());

    private static IResult ExternalResult<T>(IntegrationResult<T> r, HttpContext c) where T : class
    {
        if (r.Succeeded && r.Value is not null) return Results.Json(r.Value, statusCode: StatusCodes.Status202Accepted);
        var status = r.ErrorCode is "INTEGRATION_BATCH_SIZE_INVALID" or "INTEGRATION_EXTERNAL_EVENT_ID_INVALID" or "INTEGRATION_EVENT_INVALID" ? StatusCodes.Status400BadRequest : StatusCodes.Status422UnprocessableEntity;
        return Error(c, status, r.ErrorCode ?? "INTEGRATION_INGEST_FAILED", r.ErrorMessage ?? "Entegrasyon olayı kabul edilemedi.");
    }

    private static IResult ToResult<T>(IntegrationResult<T> r, HttpContext c, int success = 200) where T : class
    {
        if (r.Succeeded && r.Value is not null) return Results.Json(r.Value, statusCode: success);
        var code = r.ErrorCode ?? "INTEGRATION_OPERATION_FAILED";
        var status = code == "SCOPE_DENIED" ? 403
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? 404
            : code is "RECORD_MODIFIED_BY_ANOTHER_USER" or "INTEGRATION_SYSTEM_CODE_EXISTS" or "INTEGRATION_DEVICE_CODE_EXISTS" or "INTEGRATION_MAPPING_EXISTS" ? 409
            : 422;
        return Error(c, status, code, r.ErrorMessage ?? "Entegrasyon işlemi tamamlanamadı.");
    }

    private static async Task AuditAsync(AuditService audit, ILoggerFactory logs, ClaimsPrincipal p, HttpContext c, string eventType, bool succeeded, Guid? entityId, string? errorCode, string? message, CancellationToken ct)
    {
        try { await audit.WriteAsync(new AuditEvent(AuditCategories.Administration, eventType, succeeded, succeeded ? AuditSeverities.Info : AuditSeverities.Warning, Actor(p, out var actor) ? actor : null, p.FindFirstValue("unique_name"), c.Connection.RemoteIpAddress?.ToString(), c.Request.Headers["User-Agent"].ToString(), c.TraceIdentifier, "INTEGRATION", entityId?.ToString(), errorCode, message), ct); }
        catch (Exception ex) { logs.CreateLogger("IntegrationAudit").LogError(ex, "Integration audit write failed for {EventType}.", eventType); }
    }

    private static Guid? GuidValue(IQueryCollection q, string key) => Guid.TryParse(q[key].ToString(), out var v) ? v : null;
    private static int IntValue(IQueryCollection q, string key, int fallback) => int.TryParse(q[key].ToString(), out var v) ? v : fallback;
    private static string? Text(IQueryCollection q, string key) => string.IsNullOrWhiteSpace(q[key]) ? null : q[key].ToString();
    private static bool Actor(ClaimsPrincipal p, out Guid userId) => Guid.TryParse(p.FindFirstValue("sub"), out userId);
    private static IResult Unauthorized(HttpContext c) => Error(c, 401, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");
    private static IResult Error(HttpContext c, int status, string code, string message) => Results.Json(ApiErrorResponse.Create(code, message, c.TraceIdentifier), statusCode: status);
}
