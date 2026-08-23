using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Documents;

namespace PersonnelPlatform.Api.Documents;

public static class DocumentHistoryEndpoints
{
    public static IEndpointRouteBuilder MapDocumentHistoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/documents/employee-documents/{documentId:guid}", GetDocumentAsync)
            .WithTags("Documents")
            .RequireAuthorization()
            .RequirePermission(DocumentPermissions.EmployeeView);
        endpoints.MapGet("/api/v1/documents/employee-documents/{documentId:guid}/history", ListHistoryAsync)
            .WithTags("Documents")
            .RequireAuthorization()
            .RequirePermission(DocumentPermissions.EmployeeView);
        return endpoints;
    }

    private static async Task<IResult> GetDocumentAsync(Guid documentId, ClaimsPrincipal principal, DocumentHistoryService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.GetAsync(userId, documentId, ct), context, "Belge alınamadı.");
    }

    private static async Task<IResult> ListHistoryAsync(Guid documentId, ClaimsPrincipal principal, DocumentHistoryService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListAsync(userId, documentId, ct), context, "Belge geçmişi alınamadı.");
    }

    private static IResult ToResult<T>(DocumentResult<T> result, HttpContext context, string fallbackMessage) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Ok(result.Value);
        var status = result.ErrorCode == "SCOPE_DENIED" ? StatusCodes.Status403Forbidden
            : result.ErrorCode?.EndsWith("_NOT_FOUND", StringComparison.Ordinal) == true ? StatusCodes.Status404NotFound
            : StatusCodes.Status422UnprocessableEntity;
        return Error(context, status, result.ErrorCode ?? "DOCUMENT_OPERATION_FAILED", result.ErrorMessage ?? fallbackMessage);
    }

    private static bool TryActor(ClaimsPrincipal principal, out Guid userId) => Guid.TryParse(principal.FindFirstValue("sub"), out userId);
    private static IResult Unauthorized(HttpContext context) => Error(context, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");
    private static IResult Error(HttpContext context, int statusCode, string code, string message) =>
        Results.Json(ApiErrorResponse.Create(code, message, context.TraceIdentifier), statusCode: statusCode);
}
