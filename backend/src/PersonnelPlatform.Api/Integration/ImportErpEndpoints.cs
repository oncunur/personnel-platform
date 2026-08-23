using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Application.Integration;

namespace PersonnelPlatform.Api.Integration;

public static class ImportErpEndpoints
{
    public static IEndpointRouteBuilder MapImportErpEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var imports = endpoints.MapGroup("/api/v1/imports").WithTags("Imports").RequireAuthorization();
        imports.MapGet("/", ListImportsAsync).RequirePermission(ImportErpPermissions.ImportView);
        imports.MapGet("/{jobId:guid}", GetImportAsync).RequirePermission(ImportErpPermissions.ImportView);
        imports.MapGet("/{jobId:guid}/rows", ListImportRowsAsync).RequirePermission(ImportErpPermissions.ImportView);
        imports.MapPost("/upload", UploadImportAsync).RequirePermission(ImportErpPermissions.ImportManage).DisableAntiforgery();
        imports.MapPut("/{jobId:guid}/mapping", ApplyMappingAsync).RequirePermission(ImportErpPermissions.ImportManage);
        imports.MapPost("/{jobId:guid}/process", ProcessImportAsync).RequirePermission(ImportErpPermissions.ImportManage);

        var erp = endpoints.MapGroup("/api/v1/erp").WithTags("ERP").RequireAuthorization();
        erp.MapGet("/account-mappings", ListAccountMappingsAsync).RequirePermission(ImportErpPermissions.ErpAccountView);
        erp.MapPost("/account-mappings", CreateAccountMappingAsync).RequirePermission(ImportErpPermissions.ErpAccountManage);
        erp.MapPut("/account-mappings/{mappingId:guid}", UpdateAccountMappingAsync).RequirePermission(ImportErpPermissions.ErpAccountManage);
        erp.MapGet("/batches", ListBatchesAsync).RequirePermission(ImportErpPermissions.ErpBatchView);
        erp.MapPost("/batches", CreateBatchAsync).RequirePermission(ImportErpPermissions.ErpBatchManage);
        erp.MapGet("/batches/{batchId:guid}/lines", ListBatchLinesAsync).RequirePermission(ImportErpPermissions.ErpBatchView);
        erp.MapGet("/batches/{batchId:guid}/file", DownloadBatchAsync).RequirePermission(ImportErpPermissions.ErpBatchView);
        erp.MapPost("/batches/{batchId:guid}/send", SendBatchAsync).RequirePermission(ImportErpPermissions.ErpBatchManage);
        erp.MapPost("/batches/{batchId:guid}/reconcile", ReconcileBatchAsync).RequirePermission(ImportErpPermissions.ErpReconcile);
        erp.MapPost("/batches/{batchId:guid}/close", CloseBatchAsync).RequirePermission(ImportErpPermissions.ErpBatchManage);
        return endpoints;
    }

    private static async Task<IResult> ListImportsAsync(ClaimsPrincipal p, ImportErpService service, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        return ToResult(await service.ListImportsAsync(userId, GuidValue(c.Request.Query, "companyId"), IntValue(c.Request.Query, "take", 100), ct), c);
    }

    private static async Task<IResult> GetImportAsync(Guid jobId, ClaimsPrincipal p, ImportErpService service, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        return ToResult(await service.GetImportAsync(userId, jobId, ct), c);
    }

    private static async Task<IResult> ListImportRowsAsync(Guid jobId, ClaimsPrincipal p, ImportErpService service, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var errorsOnly = bool.TryParse(c.Request.Query["errorsOnly"], out var parsed) && parsed;
        return ToResult(await service.ListImportRowsAsync(userId, jobId, errorsOnly, IntValue(c.Request.Query, "take", 500), ct), c);
    }

    private static async Task<IResult> UploadImportAsync(ClaimsPrincipal p, ImportErpService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        if (!c.Request.HasFormContentType) return Error(c, 400, "IMPORT_FORM_REQUIRED", "multipart/form-data bekleniyor.");
        var form = await c.Request.ReadFormAsync(ct);
        if (!Guid.TryParse(form["companyId"], out var companyId) || !Guid.TryParse(form["integrationSystemId"], out var systemId)) return Error(c, 400, "IMPORT_CONTEXT_REQUIRED", "companyId ve integrationSystemId zorunludur.");
        var targetType = form["targetType"].ToString();
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length <= 0) return Error(c, 400, "IMPORT_FILE_REQUIRED", "Excel dosyası zorunludur.");
        if (file.Length > SpreadsheetImportReader.MaxFileBytes) return Error(c, 413, "IMPORT_FILE_TOO_LARGE", "Excel dosyası 10 MB sınırını aşıyor.");
        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream((int)Math.Min(file.Length, int.MaxValue));
        await stream.CopyToAsync(memory, ct);
        var result = await service.UploadImportAsync(userId, companyId, systemId, targetType, file.FileName, memory.ToArray(), ct);
        await AuditAsync(audit, logs, p, c, "IMPORT_FILE_UPLOADED", result.Succeeded, "IMPORT_JOB", result.Value?.Job.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, c, StatusCodes.Status201Created);
    }

    private static async Task<IResult> ApplyMappingAsync(Guid jobId, ApplyImportMappingRequest request, ClaimsPrincipal p, ImportErpService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var result = await service.ApplyImportMappingAsync(userId, jobId, request, ct);
        await AuditAsync(audit, logs, p, c, "IMPORT_MAPPING_APPLIED", result.Succeeded, "IMPORT_JOB", jobId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, c);
    }

    private static async Task<IResult> ProcessImportAsync(Guid jobId, ProcessImportRequest request, ClaimsPrincipal p, ImportErpService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var result = await service.ProcessImportAsync(userId, jobId, request, ct);
        await AuditAsync(audit, logs, p, c, "IMPORT_PROCESSED", result.Succeeded, "IMPORT_JOB", jobId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, c);
    }

    private static async Task<IResult> ListAccountMappingsAsync(ClaimsPrincipal p, ImportErpService service, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var systemId = GuidValue(c.Request.Query, "systemId");
        if (systemId is null) return Error(c, 400, "ERP_SYSTEM_REQUIRED", "systemId zorunludur.");
        return ToResult(await service.ListErpAccountMappingsAsync(userId, systemId.Value, ct), c);
    }

    private static async Task<IResult> CreateAccountMappingAsync(CreateErpAccountMappingRequest request, ClaimsPrincipal p, ImportErpService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var result = await service.CreateErpAccountMappingAsync(userId, request, ct);
        await AuditAsync(audit, logs, p, c, "ERP_ACCOUNT_MAPPING_CREATED", result.Succeeded, "ERP_ACCOUNT_MAPPING", result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, c, StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateAccountMappingAsync(Guid mappingId, UpdateErpAccountMappingRequest request, ClaimsPrincipal p, ImportErpService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var result = await service.UpdateErpAccountMappingAsync(userId, mappingId, request, ct);
        await AuditAsync(audit, logs, p, c, "ERP_ACCOUNT_MAPPING_UPDATED", result.Succeeded, "ERP_ACCOUNT_MAPPING", mappingId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, c);
    }

    private static async Task<IResult> ListBatchesAsync(ClaimsPrincipal p, ImportErpService service, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        return ToResult(await service.ListErpBatchesAsync(userId, GuidValue(c.Request.Query, "companyId"), GuidValue(c.Request.Query, "systemId"), IntValue(c.Request.Query, "take", 100), ct), c);
    }

    private static async Task<IResult> CreateBatchAsync(CreateErpBatchRequest request, ClaimsPrincipal p, ImportErpService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var result = await service.CreateErpBatchAsync(userId, request, ct);
        await AuditAsync(audit, logs, p, c, "ERP_BATCH_CREATED", result.Succeeded, "ERP_BATCH", result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, c, StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListBatchLinesAsync(Guid batchId, ClaimsPrincipal p, ImportErpService service, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        return ToResult(await service.ListErpBatchLinesAsync(userId, batchId, ct), c);
    }

    private static async Task<IResult> DownloadBatchAsync(Guid batchId, ClaimsPrincipal p, ImportErpService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var result = await service.DownloadErpBatchAsync(userId, batchId, ct);
        await AuditAsync(audit, logs, p, c, "ERP_BATCH_EXPORTED", result.Succeeded, "ERP_BATCH", batchId, result.ErrorCode, result.ErrorMessage, ct);
        if (!result.Succeeded || result.Value is null) return ToResult(result, c);
        return Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    private static async Task<IResult> SendBatchAsync(Guid batchId, ErpBatchActionRequest request, ClaimsPrincipal p, ImportErpService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var result = await service.SendErpBatchAsync(userId, batchId, request, ct);
        await AuditAsync(audit, logs, p, c, "ERP_BATCH_SENT", result.Succeeded, "ERP_BATCH", batchId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, c);
    }

    private static async Task<IResult> ReconcileBatchAsync(Guid batchId, ReconcileErpBatchRequest request, ClaimsPrincipal p, ImportErpService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var result = await service.ReconcileErpBatchAsync(userId, batchId, request, ct);
        await AuditAsync(audit, logs, p, c, "ERP_BATCH_RECONCILED", result.Succeeded, "ERP_BATCH", batchId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, c);
    }

    private static async Task<IResult> CloseBatchAsync(Guid batchId, ErpBatchActionRequest request, ClaimsPrincipal p, ImportErpService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var result = await service.CloseErpBatchAsync(userId, batchId, request, ct);
        await AuditAsync(audit, logs, p, c, "ERP_BATCH_CLOSED", result.Succeeded, "ERP_BATCH", batchId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, c);
    }

    private static IResult ToResult<T>(IntegrationResult<T> result, HttpContext c, int success = 200) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Json(result.Value, statusCode: success);
        var code = result.ErrorCode ?? "INTEGRATION_OPERATION_FAILED";
        var status = code == "SCOPE_DENIED" ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code is "RECORD_MODIFIED_BY_ANOTHER_USER" or "INTEGRATION_MAPPING_EXISTS" or "ERP_ACCOUNT_MAPPING_EXISTS" ? StatusCodes.Status409Conflict
            : code is "IMPORT_FILE_INVALID" or "IMPORT_MAPPING_REQUIRED" or "IMPORT_MAPPING_COLUMN_NOT_FOUND" or "IMPORT_MAPPING_DUPLICATE_COLUMN" or "ERP_DATE_RANGE_INVALID" ? StatusCodes.Status400BadRequest
            : StatusCodes.Status422UnprocessableEntity;
        return Error(c, status, code, result.ErrorMessage ?? "İşlem tamamlanamadı.");
    }

    private static async Task AuditAsync(AuditService audit, ILoggerFactory logs, ClaimsPrincipal p, HttpContext c, string eventType, bool succeeded, string entityType, Guid? entityId, string? errorCode, string? message, CancellationToken ct)
    {
        try { await audit.WriteAsync(new AuditEvent(AuditCategories.Administration, eventType, succeeded, succeeded ? AuditSeverities.Info : AuditSeverities.Warning, Actor(p, out var actor) ? actor : null, p.FindFirstValue("unique_name"), c.Connection.RemoteIpAddress?.ToString(), c.Request.Headers["User-Agent"].ToString(), c.TraceIdentifier, entityType, entityId?.ToString(), errorCode, message), ct); }
        catch (Exception ex) { logs.CreateLogger("ImportErpAudit").LogError(ex, "Import/ERP audit write failed for {EventType}.", eventType); }
    }

    private static Guid? GuidValue(IQueryCollection q, string key) => Guid.TryParse(q[key].ToString(), out var value) ? value : null;
    private static int IntValue(IQueryCollection q, string key, int fallback) => int.TryParse(q[key].ToString(), out var value) ? value : fallback;
    private static bool Actor(ClaimsPrincipal p, out Guid userId) => Guid.TryParse(p.FindFirstValue("sub"), out userId);
    private static IResult Unauthorized(HttpContext c) => Error(c, 401, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");
    private static IResult Error(HttpContext c, int status, string code, string message) => Results.Json(ApiErrorResponse.Create(code, message, c.TraceIdentifier), statusCode: status);
}
