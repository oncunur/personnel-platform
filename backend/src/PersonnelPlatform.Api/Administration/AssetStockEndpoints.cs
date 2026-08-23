using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Administration;
using PersonnelPlatform.Application.Audit;

namespace PersonnelPlatform.Api.Administration;

public static class AssetStockEndpoints
{
    public static IEndpointRouteBuilder MapAssetStockEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/administration").WithTags("Administration").RequireAuthorization();
        group.MapGet("/stock/locations", ListLocationsAsync).RequirePermission(AdministrationPermissions.StockView);
        group.MapPost("/stock/locations", CreateLocationAsync).RequirePermission(AdministrationPermissions.StockManage);
        group.MapGet("/stock/items", ListItemsAsync).RequirePermission(AdministrationPermissions.StockView);
        group.MapPost("/stock/items", CreateItemAsync).RequirePermission(AdministrationPermissions.StockManage);
        group.MapGet("/stock/balances", ListBalancesAsync).RequirePermission(AdministrationPermissions.StockView);
        group.MapGet("/stock/movements", ListMovementsAsync).RequirePermission(AdministrationPermissions.StockView);
        group.MapPost("/stock/movements", RecordMovementAsync).RequirePermission(AdministrationPermissions.StockMovementRecord);
        group.MapGet("/assets", ListAssetsAsync).RequirePermission(AdministrationPermissions.AssetView);
        group.MapPost("/assets", CreateAssetAsync).RequirePermission(AdministrationPermissions.AssetManage);
        group.MapPost("/assets/{assetId:guid}/assign", AssignAssetAsync).RequirePermission(AdministrationPermissions.AssetAssign);
        group.MapPost("/assets/{assetId:guid}/return", ReturnAssetAsync).RequirePermission(AdministrationPermissions.AssetAssign);
        group.MapPost("/assets/{assetId:guid}/lost", MarkLostAsync).RequirePermission(AdministrationPermissions.AssetAssign);
        group.MapGet("/asset-assignments", ListAssignmentsAsync).RequirePermission(AdministrationPermissions.AssetView);
        return endpoints;
    }

    private static async Task<IResult> ListLocationsAsync(ClaimsPrincipal principal, AssetStockService service, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); return ToResult(await service.ListLocationsAsync(userId, ReadGuid(context.Request.Query, "companyId"), ct), context); }

    private static async Task<IResult> CreateLocationAsync(CreateStockLocationRequest request, ClaimsPrincipal principal, AssetStockService service, AuditService audit, ILoggerFactory logs, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var result = await service.CreateLocationAsync(userId, request, ct); await AuditAsync(audit, logs, principal, context, "STOCK_LOCATION_CREATED", result.Succeeded, result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct); return ToResult(result, context, StatusCodes.Status201Created); }

    private static async Task<IResult> ListItemsAsync(ClaimsPrincipal principal, AssetStockService service, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); return ToResult(await service.ListStockItemsAsync(userId, ReadGuid(context.Request.Query, "companyId"), ct), context); }

    private static async Task<IResult> CreateItemAsync(CreateStockItemRequest request, ClaimsPrincipal principal, AssetStockService service, AuditService audit, ILoggerFactory logs, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var result = await service.CreateStockItemAsync(userId, request, ct); await AuditAsync(audit, logs, principal, context, "STOCK_ITEM_CREATED", result.Succeeded, result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct); return ToResult(result, context, StatusCodes.Status201Created); }

    private static async Task<IResult> ListBalancesAsync(ClaimsPrincipal principal, AssetStockService service, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); return ToResult(await service.ListBalancesAsync(userId, ReadGuid(context.Request.Query, "companyId"), ReadGuid(context.Request.Query, "itemId"), ct), context); }

    private static async Task<IResult> ListMovementsAsync(ClaimsPrincipal principal, AssetStockService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context); var q = context.Request.Query;
        return ToResult(await service.ListMovementsAsync(userId, ReadGuid(q, "companyId"), ReadGuid(q, "itemId"), ReadGuid(q, "employeeId"), ReadDateTime(q, "from"), ReadDateTime(q, "to"), ReadInt(q, "take", 100), ct), context);
    }

    private static async Task<IResult> RecordMovementAsync(CreateStockMovementRequest request, ClaimsPrincipal principal, AssetStockService service, AuditService audit, ILoggerFactory logs, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var result = await service.RecordMovementAsync(userId, request, ct); await AuditAsync(audit, logs, principal, context, "STOCK_MOVEMENT_RECORDED", result.Succeeded, result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct); return ToResult(result, context, StatusCodes.Status201Created); }

    private static async Task<IResult> ListAssetsAsync(ClaimsPrincipal principal, AssetStockService service, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); return ToResult(await service.ListAssetsAsync(userId, ReadGuid(context.Request.Query, "companyId"), ReadString(context.Request.Query, "status"), ct), context); }

    private static async Task<IResult> CreateAssetAsync(CreateAssetRequest request, ClaimsPrincipal principal, AssetStockService service, AuditService audit, ILoggerFactory logs, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var result = await service.CreateAssetAsync(userId, request, ct); await AuditAsync(audit, logs, principal, context, "ASSET_CREATED", result.Succeeded, result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct); return ToResult(result, context, StatusCodes.Status201Created); }

    private static async Task<IResult> AssignAssetAsync(Guid assetId, AssignAssetRequest request, ClaimsPrincipal principal, AssetStockService service, AuditService audit, ILoggerFactory logs, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var normalized = request with { AssetId = assetId }; var result = await service.AssignAssetAsync(userId, normalized, ct);
        await AuditAsync(audit, logs, principal, context, "ASSET_ASSIGNED", result.Succeeded, result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct); return ToResult(result, context);
    }

    private static async Task<IResult> ReturnAssetAsync(Guid assetId, ReturnAssetRequest request, ClaimsPrincipal principal, AssetStockService service, AuditService audit, ILoggerFactory logs, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var result = await service.ReturnAssetAsync(userId, assetId, request, ct); await AuditAsync(audit, logs, principal, context, "ASSET_RETURNED", result.Succeeded, result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct); return ToResult(result, context); }

    private static async Task<IResult> MarkLostAsync(Guid assetId, MarkAssetLostRequest request, ClaimsPrincipal principal, AssetStockService service, AuditService audit, ILoggerFactory logs, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var result = await service.MarkAssetLostAsync(userId, assetId, request, ct); await AuditAsync(audit, logs, principal, context, "ASSET_MARKED_LOST", result.Succeeded, result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct); return ToResult(result, context); }

    private static async Task<IResult> ListAssignmentsAsync(ClaimsPrincipal principal, AssetStockService service, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var q = context.Request.Query; return ToResult(await service.ListAssignmentsAsync(userId, ReadGuid(q, "companyId"), ReadGuid(q, "employeeId"), ReadString(q, "status"), ct), context); }

    private static IResult ToResult<T>(AdministrationResult<T> result, HttpContext context, int successStatus = StatusCodes.Status200OK) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Json(result.Value, statusCode: successStatus);
        var code = result.ErrorCode ?? "ADMINISTRATION_OPERATION_FAILED";
        var status = code == "SCOPE_DENIED" || code.EndsWith("_DENIED", StringComparison.Ordinal) ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code is "STOCK_NEGATIVE_NOT_ALLOWED" or "STOCK_EXTERNAL_EVENT_DUPLICATE" or "STOCK_LOCATION_CODE_EXISTS" or "STOCK_ITEM_CODE_EXISTS" or "ASSET_TAG_EXISTS" or "ASSET_SERIAL_EXISTS" or "ASSET_ALREADY_ASSIGNED" or "ASSET_STATE_INVALID" or "RECORD_MODIFIED_BY_ANOTHER_USER" ? StatusCodes.Status409Conflict
            : StatusCodes.Status422UnprocessableEntity;
        return Error(context, status, code, result.ErrorMessage ?? "İşlem tamamlanamadı.");
    }

    private static Guid? ReadGuid(IQueryCollection q, string key) => Guid.TryParse(q[key].ToString(), out var v) ? v : null;
    private static int ReadInt(IQueryCollection q, string key, int fallback) => int.TryParse(q[key].ToString(), out var v) ? v : fallback;
    private static DateTimeOffset? ReadDateTime(IQueryCollection q, string key) => DateTimeOffset.TryParse(q[key].ToString(), out var v) ? v : null;
    private static string? ReadString(IQueryCollection q, string key) => string.IsNullOrWhiteSpace(q[key]) ? null : q[key].ToString();
    private static bool TryActor(ClaimsPrincipal principal, out Guid userId) => Guid.TryParse(principal.FindFirstValue("sub"), out userId);
    private static IResult Unauthorized(HttpContext context) => Error(context, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");
    private static IResult Error(HttpContext context, int status, string code, string message) => Results.Json(ApiErrorResponse.Create(code, message, context.TraceIdentifier), statusCode: status);

    private static async Task AuditAsync(AuditService service, ILoggerFactory logs, ClaimsPrincipal principal, HttpContext context, string eventType, bool succeeded, Guid? entityId, string? errorCode, string? message, CancellationToken ct)
    {
        try { await service.WriteAsync(new AuditEvent(AuditCategories.Administration, eventType, succeeded, succeeded ? AuditSeverities.Info : AuditSeverities.Warning, TryActor(principal, out var actor) ? actor : null, principal.FindFirstValue("unique_name"), context.Connection.RemoteIpAddress?.ToString(), context.Request.Headers["User-Agent"].ToString(), context.TraceIdentifier, "ASSET_STOCK", entityId?.ToString(), errorCode, message), ct); }
        catch (Exception ex) { logs.CreateLogger("AssetStockAudit").LogError(ex, "Asset/stock audit write failed for {EventType}.", eventType); }
    }
}
