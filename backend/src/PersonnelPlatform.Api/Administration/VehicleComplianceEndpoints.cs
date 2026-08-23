using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Administration;
using PersonnelPlatform.Application.Audit;

namespace PersonnelPlatform.Api.Administration;

public static class VehicleComplianceEndpoints
{
    public static IEndpointRouteBuilder MapVehicleComplianceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/api/v1/administration/vehicles/{vehicleId:guid}/compliance", UpdateAsync)
            .WithTags("Administration · Vehicles")
            .RequireAuthorization()
            .RequirePermission(VehiclePermissions.Manage);
        return endpoints;
    }

    private static async Task<IResult> UpdateAsync(
        Guid vehicleId,
        UpdateVehicleComplianceRequest request,
        ClaimsPrincipal principal,
        VehicleComplianceService service,
        AuditService audit,
        ILoggerFactory logs,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue("sub"), out var userId))
            return Error(context, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");

        var result = await service.UpdateAsync(userId, vehicleId, request, cancellationToken);
        try
        {
            await audit.WriteAsync(new AuditEvent(
                AuditCategories.Administration,
                "VEHICLE_COMPLIANCE_UPDATED",
                result.Succeeded,
                result.Succeeded ? AuditSeverities.Info : AuditSeverities.Warning,
                userId,
                principal.FindFirstValue("unique_name"),
                context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers["User-Agent"].ToString(),
                context.TraceIdentifier,
                "VEHICLE",
                vehicleId.ToString(),
                result.ErrorCode,
                result.ErrorMessage), cancellationToken);
        }
        catch (Exception ex)
        {
            logs.CreateLogger("VehicleComplianceAudit").LogError(ex, "Vehicle compliance audit write failed.");
        }

        if (result.Succeeded && result.Value is not null) return Results.Ok(result.Value);
        var code = result.ErrorCode ?? "VEHICLE_COMPLIANCE_UPDATE_FAILED";
        var status = code == "SCOPE_DENIED" ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code == "RECORD_MODIFIED_BY_ANOTHER_USER" ? StatusCodes.Status409Conflict
            : StatusCodes.Status422UnprocessableEntity;
        return Error(context, status, code, result.ErrorMessage ?? "Araç uygunluk tarihleri güncellenemedi.");
    }

    private static IResult Error(HttpContext context, int status, string code, string message) =>
        Results.Json(ApiErrorResponse.Create(code, message, context.TraceIdentifier), statusCode: status);
}
