using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Documents;

namespace PersonnelPlatform.Api.Documents;

public static class DocumentIntelligenceEndpoints
{
    public static IEndpointRouteBuilder MapDocumentIntelligenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/documents").WithTags("Documents").RequireAuthorization();
        group.MapGet("/expiring", ListExpiringAsync).RequirePermission(DocumentPermissions.ExpiringView);
        group.MapGet("/expired", ListExpiredAsync).RequirePermission(DocumentPermissions.ExpiringView);
        group.MapGet("/missing", ListMissingAsync).RequirePermission(DocumentPermissions.MissingView);
        return endpoints;
    }

    private static async Task<IResult> ListExpiringAsync(ClaimsPrincipal principal, DocumentIntelligenceService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var days = ReadInt(context.Request.Query, "days", 30);
        var limit = ReadInt(context.Request.Query, "limit", 100);
        return ToResult(await service.ListExpiringAsync(userId, days, limit, ct), context);
    }

    private static async Task<IResult> ListExpiredAsync(ClaimsPrincipal principal, DocumentIntelligenceService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var limit = ReadInt(context.Request.Query, "limit", 100);
        return ToResult(await service.ListExpiredAsync(userId, limit, ct), context);
    }

    private static async Task<IResult> ListMissingAsync(ClaimsPrincipal principal, DocumentIntelligenceService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var limit = ReadInt(context.Request.Query, "limit", 100);
        return ToResult(await service.ListMissingAsync(userId, limit, ct), context);
    }

    private static int ReadInt(IQueryCollection query, string key, int fallback) => int.TryParse(query[key].ToString(), out var value) ? value : fallback;
    private static bool TryActor(ClaimsPrincipal principal, out Guid userId) => Guid.TryParse(principal.FindFirstValue("sub"), out userId);
    private static IResult Unauthorized(HttpContext context) => Error(context, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");

    private static IResult ToResult<T>(DocumentResult<T> result, HttpContext context) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Ok(result.Value);
        return Error(context, StatusCodes.Status422UnprocessableEntity, result.ErrorCode ?? "DOCUMENT_OPERATION_FAILED", result.ErrorMessage ?? "İşlem tamamlanamadı.");
    }

    private static IResult Error(HttpContext context, int statusCode, string code, string message) =>
        Results.Json(ApiErrorResponse.Create(code, message, context.TraceIdentifier), statusCode: statusCode);
}
