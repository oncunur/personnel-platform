using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Documents;

namespace PersonnelPlatform.Api.Documents;

public static class DocumentHistoryEndpoints
{
    public static IEndpointRouteBuilder MapDocumentHistoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/documents/employee-documents/{documentId:guid}/history", ListHistoryAsync)
            .WithTags("Documents")
            .RequireAuthorization()
            .RequirePermission(DocumentPermissions.EmployeeView);
        return endpoints;
    }

    private static async Task<IResult> ListHistoryAsync(Guid documentId, ClaimsPrincipal principal, DocumentHistoryService service, HttpContext context, CancellationToken ct)
    {
        if (!Guid.TryParse(principal.FindFirstValue("sub"), out var userId))
            return Error(context, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");

        var result = await service.ListAsync(userId, documentId, ct);
        if (result.Succeeded && result.Value is not null) return Results.Ok(result.Value);
        var status = result.ErrorCode == "SCOPE_DENIED" ? StatusCodes.Status403Forbidden
            : result.ErrorCode?.EndsWith("_NOT_FOUND", StringComparison.Ordinal) == true ? StatusCodes.Status404NotFound
            : StatusCodes.Status422UnprocessableEntity;
        return Error(context, status, result.ErrorCode ?? "DOCUMENT_HISTORY_FAILED", result.ErrorMessage ?? "Belge geçmişi alınamadı.");
    }

    private static IResult Error(HttpContext context, int statusCode, string code, string message) =>
        Results.Json(ApiErrorResponse.Create(code, message, context.TraceIdentifier), statusCode: statusCode);
}
