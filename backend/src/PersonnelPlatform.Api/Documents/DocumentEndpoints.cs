using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Application.Documents;

namespace PersonnelPlatform.Api.Documents;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/documents").WithTags("Documents").RequireAuthorization();
        group.MapGet("/types", ListTypesAsync).RequirePermission(DocumentPermissions.TypeView);
        group.MapPost("/types", CreateTypeAsync).RequirePermission(DocumentPermissions.TypeManage);
        group.MapGet("/employees/{employeeId:guid}", ListEmployeeDocumentsAsync).RequirePermission(DocumentPermissions.EmployeeView);
        group.MapGet("/employees/{employeeId:guid}/missing", ListMissingDocumentsAsync).RequirePermission(DocumentPermissions.MissingView);
        group.MapPost("/employees/{employeeId:guid}", UploadEmployeeDocumentAsync).RequirePermission(DocumentPermissions.EmployeeUpload).DisableAntiforgery();
        group.MapPost("/employee-documents/{documentId:guid}/renew", RenewEmployeeDocumentAsync).RequirePermission(DocumentPermissions.EmployeeRenew).DisableAntiforgery();
        group.MapPost("/employee-documents/{documentId:guid}/cancel", CancelEmployeeDocumentAsync).RequirePermission(DocumentPermissions.EmployeeCancel);
        group.MapGet("/employee-documents/{documentId:guid}/file", OpenEmployeeDocumentFileAsync).RequirePermission(DocumentPermissions.FileView);
        return endpoints;
    }

    private static async Task<IResult> ListTypesAsync(DocumentService service, CancellationToken ct) => Results.Ok(await service.ListDocumentTypesAsync(ct));

    private static async Task<IResult> CreateTypeAsync(CreateDocumentTypeRequest request, ClaimsPrincipal principal, DocumentService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreateDocumentTypeAsync(userId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "DOCUMENT_TYPE_CREATED", result.Succeeded, "DOCUMENT_TYPE", result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListEmployeeDocumentsAsync(Guid employeeId, ClaimsPrincipal principal, DocumentService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListEmployeeDocumentsAsync(userId, employeeId, ct), context);
    }

    private static async Task<IResult> ListMissingDocumentsAsync(Guid employeeId, ClaimsPrincipal principal, DocumentService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListMissingDocumentsAsync(userId, employeeId, ct), context);
    }

    private static async Task<IResult> UploadEmployeeDocumentAsync(Guid employeeId, ClaimsPrincipal principal, DocumentService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var parsed = await ReadUploadFormAsync(context.Request, requireDocumentType: true, ct);
        if (parsed.Error is not null) return Error(context, StatusCodes.Status400BadRequest, "DOCUMENT_UPLOAD_INVALID", parsed.Error);
        await using var stream = parsed.File?.OpenReadStream();
        var request = new UploadEmployeeDocumentRequest(parsed.DocumentTypeId, parsed.DocumentNumber, parsed.IssueDate, parsed.ValidFrom, parsed.ValidUntil, parsed.IssuingAuthority, parsed.CountryCode, parsed.Notes,
            parsed.File is null ? null : new DocumentUploadFile(parsed.File.FileName, parsed.File.ContentType, parsed.File.Length, stream!));
        var result = await service.UploadAsync(userId, employeeId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "EMPLOYEE_DOCUMENT_UPLOADED", result.Succeeded, "EMPLOYEE", employeeId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> RenewEmployeeDocumentAsync(Guid documentId, ClaimsPrincipal principal, DocumentService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var parsed = await ReadUploadFormAsync(context.Request, requireDocumentType: false, ct);
        if (parsed.Error is not null) return Error(context, StatusCodes.Status400BadRequest, "DOCUMENT_UPLOAD_INVALID", parsed.Error);
        await using var stream = parsed.File?.OpenReadStream();
        var request = new UploadEmployeeDocumentRequest(Guid.Empty, parsed.DocumentNumber, parsed.IssueDate, parsed.ValidFrom, parsed.ValidUntil, parsed.IssuingAuthority, parsed.CountryCode, parsed.Notes,
            parsed.File is null ? null : new DocumentUploadFile(parsed.File.FileName, parsed.File.ContentType, parsed.File.Length, stream!));
        var result = await service.RenewAsync(userId, documentId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "EMPLOYEE_DOCUMENT_RENEWED", result.Succeeded, "EMPLOYEE_DOCUMENT", documentId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> CancelEmployeeDocumentAsync(Guid documentId, ClaimsPrincipal principal, DocumentService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CancelAsync(userId, documentId, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "EMPLOYEE_DOCUMENT_CANCELLED", result.Succeeded, "EMPLOYEE_DOCUMENT", documentId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context);
    }

    private static async Task<IResult> OpenEmployeeDocumentFileAsync(Guid documentId, ClaimsPrincipal principal, DocumentService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.OpenFileAsync(userId, documentId, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "EMPLOYEE_DOCUMENT_FILE_VIEWED", result.Succeeded, "EMPLOYEE_DOCUMENT", documentId, result.ErrorCode, result.ErrorMessage, ct);
        if (!result.Succeeded || result.Value is null) return ToResult(result, context);
        return Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: true);
    }

    private static async Task<UploadForm> ReadUploadFormAsync(HttpRequest request, bool requireDocumentType, CancellationToken ct)
    {
        if (!request.HasFormContentType) return UploadForm.Fail("multipart/form-data bekleniyor.");
        var form = await request.ReadFormAsync(ct);
        Guid documentTypeId = Guid.Empty;
        if (requireDocumentType && !Guid.TryParse(form["documentTypeId"].ToString(), out documentTypeId)) return UploadForm.Fail("Belge türü zorunludur.");
        if (!TryDate(form["issueDate"].ToString(), out var issueDate)) return UploadForm.Fail("Düzenlenme tarihi geçersiz.");
        if (!TryDate(form["validFrom"].ToString(), out var validFrom)) return UploadForm.Fail("Geçerlilik başlangıcı geçersiz.");
        if (!TryDate(form["validUntil"].ToString(), out var validUntil)) return UploadForm.Fail("Geçerlilik bitişi geçersiz.");
        var file = form.Files.GetFile("file");
        return new UploadForm(documentTypeId, EmptyToNull(form["documentNumber"].ToString()), issueDate, validFrom, validUntil,
            EmptyToNull(form["issuingAuthority"].ToString()), EmptyToNull(form["countryCode"].ToString()), EmptyToNull(form["notes"].ToString()), file, null);
    }

    private static bool TryDate(string raw, out DateOnly? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        if (!DateOnly.TryParse(raw, out var parsed)) return false;
        value = parsed;
        return true;
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool TryActor(ClaimsPrincipal principal, out Guid userId) => Guid.TryParse(principal.FindFirstValue("sub"), out userId);
    private static IResult Unauthorized(HttpContext context) => Error(context, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");

    private static IResult ToResult<T>(DocumentResult<T> result, HttpContext context, int successStatus = StatusCodes.Status200OK) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Json(result.Value, statusCode: successStatus);
        var code = result.ErrorCode ?? "DOCUMENT_OPERATION_FAILED";
        var status = code == "SCOPE_DENIED" ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code == "FILE_SIZE_LIMIT_EXCEEDED" ? StatusCodes.Status413PayloadTooLarge
            : code is "DOCUMENT_TYPE_ALREADY_EXISTS" or "DOCUMENT_MULTIPLE_NOT_ALLOWED" ? StatusCodes.Status409Conflict
            : StatusCodes.Status422UnprocessableEntity;
        return Error(context, status, code, result.ErrorMessage ?? "İşlem tamamlanamadı.");
    }

    private static async Task AuditAsync(AuditService service, ILoggerFactory loggerFactory, ClaimsPrincipal principal, HttpContext context, string eventType, bool succeeded, string entityType, Guid? entityId, string? errorCode, string? message, CancellationToken ct)
    {
        try
        {
            await service.WriteAsync(new AuditEvent(AuditCategories.Administration, eventType, succeeded, succeeded ? AuditSeverities.Info : AuditSeverities.Warning,
                TryActor(principal, out var actor) ? actor : null, principal.FindFirstValue("unique_name"), context.Connection.RemoteIpAddress?.ToString(), context.Request.Headers["User-Agent"].ToString(), context.TraceIdentifier,
                entityType, entityId?.ToString(), errorCode, message), ct);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("DocumentAudit").LogError(exception, "Document audit write failed for {EventType}.", eventType);
        }
    }

    private sealed record UploadForm(Guid DocumentTypeId, string? DocumentNumber, DateOnly? IssueDate, DateOnly? ValidFrom, DateOnly? ValidUntil, string? IssuingAuthority, string? CountryCode, string? Notes, IFormFile? File, string? Error)
    {
        public static UploadForm Fail(string error) => new(Guid.Empty, null, null, null, null, null, null, null, null, error);
    }
}
