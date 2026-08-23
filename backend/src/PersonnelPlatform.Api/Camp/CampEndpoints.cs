using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Application.Camp;

namespace PersonnelPlatform.Api.Camp;

public static class CampEndpoints
{
    public static IEndpointRouteBuilder MapCampEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/camp").WithTags("Camp").RequireAuthorization();

        group.MapGet("/sites", ListCampsAsync).RequirePermission(CampPermissions.SiteView);
        group.MapPost("/sites", CreateCampAsync).RequirePermission(CampPermissions.SiteManage);
        group.MapGet("/sites/{campId:guid}/rooms", ListRoomsAsync).RequirePermission(CampPermissions.SiteView);
        group.MapPost("/sites/{campId:guid}/rooms", CreateRoomAsync).RequirePermission(CampPermissions.SiteManage);
        group.MapGet("/rooms/{roomId:guid}/beds", ListBedsAsync).RequirePermission(CampPermissions.SiteView);
        group.MapPost("/rooms/{roomId:guid}/beds", CreateBedAsync).RequirePermission(CampPermissions.SiteManage);
        group.MapGet("/sites/{campId:guid}/rates", ListRatesAsync).RequirePermission(CampPermissions.RateView);
        group.MapPost("/sites/{campId:guid}/rates", CreateRateAsync).RequirePermission(CampPermissions.RateManage);
        group.MapGet("/stays", SearchStaysAsync).RequirePermission(CampPermissions.StayView);
        group.MapPost("/stays", CreateStayAsync).RequirePermission(CampPermissions.StayManage);
        group.MapPost("/stays/{stayId:guid}/close", CloseStayAsync).RequirePermission(CampPermissions.StayManage);
        group.MapPost("/stays/{stayId:guid}/cancel", CancelStayAsync).RequirePermission(CampPermissions.StayManage);
        return endpoints;
    }

    private static async Task<IResult> ListCampsAsync(ClaimsPrincipal principal, CampService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListCampsAsync(userId, ct), context);
    }

    private static async Task<IResult> CreateCampAsync(CreateCampSiteRequest request, ClaimsPrincipal principal, CampService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreateCampAsync(userId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "CAMP_CREATED", result.Succeeded, result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListRoomsAsync(Guid campId, ClaimsPrincipal principal, CampService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListRoomsAsync(userId, campId, ct), context);
    }

    private static async Task<IResult> CreateRoomAsync(Guid campId, CreateCampRoomRequest request, ClaimsPrincipal principal, CampService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreateRoomAsync(userId, campId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "CAMP_ROOM_CREATED", result.Succeeded, result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListBedsAsync(Guid roomId, ClaimsPrincipal principal, CampService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListBedsAsync(userId, roomId, ct), context);
    }

    private static async Task<IResult> CreateBedAsync(Guid roomId, CreateCampBedRequest request, ClaimsPrincipal principal, CampService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreateBedAsync(userId, roomId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "CAMP_BED_CREATED", result.Succeeded, result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListRatesAsync(Guid campId, ClaimsPrincipal principal, CampService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListRatesAsync(userId, campId, ct), context);
    }

    private static async Task<IResult> CreateRateAsync(Guid campId, CreateAccommodationRateRequest request, ClaimsPrincipal principal, CampService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreateRateAsync(userId, campId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "CAMP_RATE_CREATED", result.Succeeded, result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> SearchStaysAsync(ClaimsPrincipal principal, CampService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var q = context.Request.Query;
        var query = new AccommodationStayQuery(
            ReadGuid(q, "employeeId"),
            ReadGuid(q, "campId"),
            ReadString(q, "status"),
            ReadDate(q, "from"),
            ReadDate(q, "to"),
            ReadInt(q, "page", 1),
            ReadInt(q, "pageSize", 50));
        return ToResult(await service.SearchStaysAsync(userId, query, ct), context);
    }

    private static async Task<IResult> CreateStayAsync(CreateAccommodationStayRequest request, ClaimsPrincipal principal, CampService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreateStayAsync(userId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "CAMP_STAY_CREATED", result.Succeeded, result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> CloseStayAsync(Guid stayId, CloseAccommodationStayRequest request, ClaimsPrincipal principal, CampService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CloseStayAsync(userId, stayId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "CAMP_STAY_CLOSED", result.Succeeded, stayId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context);
    }

    private static async Task<IResult> CancelStayAsync(Guid stayId, CancelAccommodationStayRequest request, ClaimsPrincipal principal, CampService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CancelStayAsync(userId, stayId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "CAMP_STAY_CANCELLED", result.Succeeded, stayId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context);
    }

    private static IResult ToResult<T>(CampResult<T> result, HttpContext context, int successStatus = StatusCodes.Status200OK) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Json(result.Value, statusCode: successStatus);
        var code = result.ErrorCode ?? "CAMP_OPERATION_FAILED";
        var status = code == "SCOPE_DENIED" || code.EndsWith("_DENIED", StringComparison.Ordinal)
            ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code is "RECORD_MODIFIED_BY_ANOTHER_USER" or "CAMP_STAY_NOT_ACTIVE"
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status422UnprocessableEntity;
        return Error(context, status, code, result.ErrorMessage ?? "İşlem tamamlanamadı.");
    }

    private static Guid? ReadGuid(IQueryCollection query, string key) => Guid.TryParse(query[key].ToString(), out var value) ? value : null;
    private static DateOnly? ReadDate(IQueryCollection query, string key) => DateOnly.TryParse(query[key].ToString(), out var value) ? value : null;
    private static int ReadInt(IQueryCollection query, string key, int fallback) => int.TryParse(query[key].ToString(), out var value) ? value : fallback;
    private static string? ReadString(IQueryCollection query, string key) => string.IsNullOrWhiteSpace(query[key].ToString()) ? null : query[key].ToString();
    private static bool TryActor(ClaimsPrincipal principal, out Guid userId) => Guid.TryParse(principal.FindFirstValue("sub"), out userId);
    private static IResult Unauthorized(HttpContext context) => Error(context, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");
    private static IResult Error(HttpContext context, int statusCode, string code, string message) => Results.Json(ApiErrorResponse.Create(code, message, context.TraceIdentifier), statusCode: statusCode);

    private static async Task AuditAsync(AuditService service, ILoggerFactory loggerFactory, ClaimsPrincipal principal, HttpContext context, string eventType, bool succeeded, Guid? entityId, string? errorCode, string? message, CancellationToken ct)
    {
        try
        {
            await service.WriteAsync(new AuditEvent(
                AuditCategories.Administration,
                eventType,
                succeeded,
                succeeded ? AuditSeverities.Info : AuditSeverities.Warning,
                TryActor(principal, out var actor) ? actor : null,
                principal.FindFirstValue("unique_name"),
                context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers["User-Agent"].ToString(),
                context.TraceIdentifier,
                "CAMP",
                entityId?.ToString(),
                errorCode,
                message), ct);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("CampAudit").LogError(exception, "Camp audit write failed for {EventType}.", eventType);
        }
    }
}
