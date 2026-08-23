using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Application.Meal;

namespace PersonnelPlatform.Api.Meal;

public static class MealEndpoints
{
    public static IEndpointRouteBuilder MapMealEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/meal").WithTags("Meal").RequireAuthorization();
        group.MapGet("/types", ListTypesAsync).RequirePermission(MealPermissions.TypeView);
        group.MapGet("/rates", ListRatesAsync).RequirePermission(MealPermissions.RateView);
        group.MapPost("/rates", CreateRateAsync).RequirePermission(MealPermissions.RateManage);
        group.MapGet("/consumptions", SearchConsumptionsAsync).RequirePermission(MealPermissions.ConsumptionView);
        group.MapPost("/consumptions", RecordConsumptionAsync).RequirePermission(MealPermissions.ConsumptionRecord);
        return endpoints;
    }

    private static async Task<IResult> ListTypesAsync(ClaimsPrincipal principal, MealService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListMealTypesAsync(userId, ct), context);
    }

    private static async Task<IResult> ListRatesAsync(ClaimsPrincipal principal, MealService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        if (!Guid.TryParse(context.Request.Query["campId"].ToString(), out var campId))
            return Error(context, StatusCodes.Status422UnprocessableEntity, "CAMP_ID_REQUIRED", "Kamp seçilmelidir.");
        var mealTypeId = ReadGuid(context.Request.Query, "mealTypeId");
        return ToResult(await service.ListRatesAsync(userId, campId, mealTypeId, ct), context);
    }

    private static async Task<IResult> CreateRateAsync(CreateMealRateRequest request, ClaimsPrincipal principal, MealService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreateRateAsync(userId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "MEAL_RATE_CREATED", result.Succeeded, result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> SearchConsumptionsAsync(ClaimsPrincipal principal, MealService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var q = context.Request.Query;
        var query = new MealConsumptionQuery(
            ReadGuid(q, "employeeId"),
            ReadGuid(q, "campId"),
            ReadGuid(q, "mealTypeId"),
            ReadDate(q, "from"),
            ReadDate(q, "to"),
            ReadInt(q, "page", 1),
            ReadInt(q, "pageSize", 50));
        return ToResult(await service.SearchConsumptionsAsync(userId, query, ct), context);
    }

    private static async Task<IResult> RecordConsumptionAsync(CreateMealConsumptionRequest request, ClaimsPrincipal principal, MealService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.RecordConsumptionAsync(userId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "MEAL_CONSUMPTION_RECORDED", result.Succeeded, result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static IResult ToResult<T>(MealResult<T> result, HttpContext context, int successStatus = StatusCodes.Status200OK) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Json(result.Value, statusCode: successStatus);
        var code = result.ErrorCode ?? "MEAL_OPERATION_FAILED";
        var status = code == "SCOPE_DENIED" || code.EndsWith("_DENIED", StringComparison.Ordinal)
            ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code is "MEAL_ALREADY_CONSUMED" or "MEAL_EXTERNAL_EVENT_DUPLICATE" or "MEAL_RATE_DATE_CONFLICT"
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status422UnprocessableEntity;
        return Error(context, status, code, result.ErrorMessage ?? "İşlem tamamlanamadı.");
    }

    private static Guid? ReadGuid(IQueryCollection query, string key) => Guid.TryParse(query[key].ToString(), out var value) ? value : null;
    private static DateOnly? ReadDate(IQueryCollection query, string key) => DateOnly.TryParse(query[key].ToString(), out var value) ? value : null;
    private static int ReadInt(IQueryCollection query, string key, int fallback) => int.TryParse(query[key].ToString(), out var value) ? value : fallback;
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
                "MEAL",
                entityId?.ToString(),
                errorCode,
                message), ct);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("MealAudit").LogError(exception, "Meal audit write failed for {EventType}.", eventType);
        }
    }
}
