using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Administration;
using PersonnelPlatform.Application.Audit;

namespace PersonnelPlatform.Api.Administration;

public static class VehicleEndpoints
{
    public static IEndpointRouteBuilder MapVehicleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/administration/vehicles").WithTags("Administration · Vehicles").RequireAuthorization();
        group.MapGet("/", ListVehiclesAsync).RequirePermission(VehiclePermissions.View);
        group.MapPost("/", CreateVehicleAsync).RequirePermission(VehiclePermissions.Manage);
        group.MapPost("/{vehicleId:guid}/status", SetStatusAsync).RequirePermission(VehiclePermissions.Manage);
        group.MapGet("/assignments", ListAssignmentsAsync).RequirePermission(VehiclePermissions.View);
        group.MapPost("/{vehicleId:guid}/assignments", AssignAsync).RequirePermission(VehiclePermissions.Assign);
        group.MapPost("/assignments/{assignmentId:guid}/close", CloseAssignmentAsync).RequirePermission(VehiclePermissions.Assign);
        group.MapGet("/{vehicleId:guid}/odometer", ListOdometerAsync).RequirePermission(VehiclePermissions.View);
        group.MapPost("/{vehicleId:guid}/odometer", RecordOdometerAsync).RequirePermission(VehiclePermissions.OdometerRecord);
        group.MapGet("/{vehicleId:guid}/maintenance", ListMaintenanceAsync).RequirePermission(VehiclePermissions.View);
        group.MapPost("/{vehicleId:guid}/maintenance", CreateMaintenanceAsync).RequirePermission(VehiclePermissions.MaintenanceManage);
        group.MapGet("/{vehicleId:guid}/fuel", ListFuelAsync).RequirePermission(VehiclePermissions.View);
        group.MapPost("/{vehicleId:guid}/fuel", CreateFuelAsync).RequirePermission(VehiclePermissions.FuelRecord);
        return endpoints;
    }

    private static async Task<IResult> ListVehiclesAsync(ClaimsPrincipal principal, VehicleService service, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var q = context.Request.Query; return ToResult(await service.ListVehiclesAsync(userId, ReadGuid(q, "companyId"), ReadString(q, "status"), ct), context); }

    private static async Task<IResult> CreateVehicleAsync(CreateVehicleRequest request, ClaimsPrincipal principal, VehicleService service, AuditService audit, ILoggerFactory logs, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var result = await service.CreateVehicleAsync(userId, request, ct); await AuditAsync(audit, logs, principal, context, "VEHICLE_CREATED", result, result.Value?.Id, ct); return ToResult(result, context, StatusCodes.Status201Created); }

    private static async Task<IResult> SetStatusAsync(Guid vehicleId, SetVehicleStatusRequest request, ClaimsPrincipal principal, VehicleService service, AuditService audit, ILoggerFactory logs, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var result = await service.SetStatusAsync(userId, vehicleId, request, ct); await AuditAsync(audit, logs, principal, context, "VEHICLE_STATUS_CHANGED", result, vehicleId, ct); return ToResult(result, context); }

    private static async Task<IResult> AssignAsync(Guid vehicleId, AssignVehicleRequest request, ClaimsPrincipal principal, VehicleService service, AuditService audit, ILoggerFactory logs, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var result = await service.AssignAsync(userId, request with { VehicleId = vehicleId }, ct); await AuditAsync(audit, logs, principal, context, "VEHICLE_ASSIGNED", result, result.Value?.Id, ct); return ToResult(result, context, StatusCodes.Status201Created); }

    private static async Task<IResult> CloseAssignmentAsync(Guid assignmentId, CloseVehicleAssignmentRequest request, ClaimsPrincipal principal, VehicleService service, AuditService audit, ILoggerFactory logs, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var result = await service.CloseAssignmentAsync(userId, assignmentId, request, ct); await AuditAsync(audit, logs, principal, context, "VEHICLE_ASSIGNMENT_CLOSED", result, assignmentId, ct); return ToResult(result, context); }

    private static async Task<IResult> ListAssignmentsAsync(ClaimsPrincipal principal, VehicleService service, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var q = context.Request.Query; return ToResult(await service.ListAssignmentsAsync(userId, ReadGuid(q, "companyId"), ReadGuid(q, "vehicleId"), ReadGuid(q, "employeeId"), ReadString(q, "status"), ct), context); }

    private static async Task<IResult> RecordOdometerAsync(Guid vehicleId, RecordOdometerRequest request, ClaimsPrincipal principal, VehicleService service, AuditService audit, ILoggerFactory logs, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var result = await service.RecordOdometerAsync(userId, vehicleId, request, ct); await AuditAsync(audit, logs, principal, context, "VEHICLE_ODOMETER_RECORDED", result, result.Value?.Id, ct); return ToResult(result, context, StatusCodes.Status201Created); }

    private static async Task<IResult> ListOdometerAsync(Guid vehicleId, ClaimsPrincipal principal, VehicleService service, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); return ToResult(await service.ListOdometerAsync(userId, vehicleId, ReadInt(context.Request.Query, "take", 100), ct), context); }

    private static async Task<IResult> CreateMaintenanceAsync(Guid vehicleId, CreateMaintenanceRequest request, ClaimsPrincipal principal, VehicleService service, AuditService audit, ILoggerFactory logs, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var result = await service.CreateMaintenanceAsync(userId, vehicleId, request, ct); await AuditAsync(audit, logs, principal, context, "VEHICLE_MAINTENANCE_RECORDED", result, result.Value?.Id, ct); return ToResult(result, context, StatusCodes.Status201Created); }

    private static async Task<IResult> ListMaintenanceAsync(Guid vehicleId, ClaimsPrincipal principal, VehicleService service, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); return ToResult(await service.ListMaintenanceAsync(userId, vehicleId, ReadInt(context.Request.Query, "take", 100), ct), context); }

    private static async Task<IResult> CreateFuelAsync(Guid vehicleId, CreateFuelRecordRequest request, ClaimsPrincipal principal, VehicleService service, AuditService audit, ILoggerFactory logs, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var result = await service.CreateFuelAsync(userId, vehicleId, request, ct); await AuditAsync(audit, logs, principal, context, "VEHICLE_FUEL_RECORDED", result, result.Value?.Id, ct); return ToResult(result, context, StatusCodes.Status201Created); }

    private static async Task<IResult> ListFuelAsync(Guid vehicleId, ClaimsPrincipal principal, VehicleService service, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); return ToResult(await service.ListFuelAsync(userId, vehicleId, ReadInt(context.Request.Query, "take", 100), ct), context); }

    private static IResult ToResult<T>(VehicleResult<T> result, HttpContext context, int successStatus = StatusCodes.Status200OK) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Json(result.Value, statusCode: successStatus);
        var code = result.ErrorCode ?? "VEHICLE_OPERATION_FAILED";
        var status = code == "SCOPE_DENIED" ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code is "VEHICLE_PLATE_EXISTS" or "VEHICLE_VIN_EXISTS" or "VEHICLE_ASSIGNMENT_DATE_CONFLICT" or "VEHICLE_ODOMETER_REGRESSION" or "VEHICLE_EXTERNAL_EVENT_DUPLICATE" or "VEHICLE_FUEL_EXTERNAL_EVENT_DUPLICATE" or "VEHICLE_LEDGER_IMMUTABLE" or "RECORD_MODIFIED_BY_ANOTHER_USER" ? StatusCodes.Status409Conflict
            : StatusCodes.Status422UnprocessableEntity;
        return Error(context, status, code, result.ErrorMessage ?? "İşlem tamamlanamadı.");
    }

    private static async Task AuditAsync<T>(AuditService audit, ILoggerFactory logs, ClaimsPrincipal principal, HttpContext context, string eventType, VehicleResult<T> result, Guid? entityId, CancellationToken ct) where T : class
    {
        try { await audit.WriteAsync(new AuditEvent(AuditCategories.Administration, eventType, result.Succeeded, result.Succeeded ? AuditSeverities.Info : AuditSeverities.Warning, TryActor(principal, out var actor) ? actor : null, principal.FindFirstValue("unique_name"), context.Connection.RemoteIpAddress?.ToString(), context.Request.Headers["User-Agent"].ToString(), context.TraceIdentifier, "VEHICLE", entityId?.ToString(), result.ErrorCode, result.ErrorMessage), ct); }
        catch (Exception ex) { logs.CreateLogger("VehicleAudit").LogError(ex, "Vehicle audit write failed for {EventType}.", eventType); }
    }

    private static Guid? ReadGuid(IQueryCollection q, string key) => Guid.TryParse(q[key].ToString(), out var v) ? v : null;
    private static int ReadInt(IQueryCollection q, string key, int fallback) => int.TryParse(q[key].ToString(), out var v) ? v : fallback;
    private static string? ReadString(IQueryCollection q, string key) => string.IsNullOrWhiteSpace(q[key]) ? null : q[key].ToString();
    private static bool TryActor(ClaimsPrincipal principal, out Guid userId) => Guid.TryParse(principal.FindFirstValue("sub"), out userId);
    private static IResult Unauthorized(HttpContext context) => Error(context, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");
    private static IResult Error(HttpContext context, int status, string code, string message) => Results.Json(ApiErrorResponse.Create(code, message, context.TraceIdentifier), statusCode: status);
}
