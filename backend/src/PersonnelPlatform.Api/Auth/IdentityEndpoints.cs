using System.Security.Claims;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Application.Identity;

namespace PersonnelPlatform.Api.Auth;

public static class IdentityEndpoints
{
    private const string RefreshCookieName = "pp_refresh";

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth").WithTags("Authentication");
        group.MapPost("/login", LoginAsync);
        group.MapPost("/refresh", RefreshAsync);
        group.MapPost("/logout", LogoutAsync);
        group.MapPost("/logout-all", LogoutAllAsync).RequireAuthorization();
        group.MapGet("/me", MeAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        AuthService authService,
        AuditService auditService,
        ILoggerFactory loggerFactory,
        HttpContext context,
        IWebHostEnvironment environment,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
            return Error(context, 400, "AUTH_VALIDATION_ERROR", "Kullanıcı adı ve parola zorunludur.");

        if (request.Username.Length > 100 || request.Password.Length > 512)
            return Error(context, 400, "AUTH_VALIDATION_ERROR", "Giriş bilgileri izin verilen uzunluğu aşıyor.");

        var result = await authService.LoginAsync(
            request.Username,
            request.Password,
            GetIpAddress(context),
            GetDeviceName(context),
            ct);

        await TryAuditAsync(
            auditService,
            loggerFactory,
            new AuditEvent(
                AuditCategories.Security,
                result.Succeeded ? "AUTH_LOGIN_SUCCEEDED" : "AUTH_LOGIN_FAILED",
                result.Succeeded,
                result.Succeeded ? AuditSeverities.Info : AuditSeverities.Warning,
                result.User?.Id,
                result.User?.Username ?? request.Username,
                GetIpAddress(context),
                GetDeviceName(context),
                context.TraceIdentifier,
                "USER",
                result.User?.Id.ToString(),
                result.ErrorCode,
                result.ErrorMessage),
            ct);

        if (!result.Succeeded || result.User is null || result.Tokens is null)
            return Error(context, 401, result.ErrorCode ?? "AUTH_INVALID_CREDENTIALS", result.ErrorMessage ?? "Kimlik doğrulama başarısız.");

        SetRefreshCookie(context, environment, result.Tokens.RefreshToken, result.Tokens.RefreshTokenExpiresAt);
        return Results.Ok(new AuthResponse(
            result.User.Id,
            result.User.Username,
            result.User.Email,
            result.Tokens.AccessToken,
            result.Tokens.AccessTokenExpiresAt));
    }

    private static async Task<IResult> RefreshAsync(
        AuthService authService,
        AuditService auditService,
        ILoggerFactory loggerFactory,
        HttpContext context,
        IWebHostEnvironment environment,
        CancellationToken ct)
    {
        if (!context.Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken))
        {
            await TryAuditAsync(
                auditService,
                loggerFactory,
                new AuditEvent(
                    AuditCategories.Security,
                    "AUTH_REFRESH_FAILED",
                    false,
                    AuditSeverities.Warning,
                    IpAddress: GetIpAddress(context),
                    UserAgent: GetDeviceName(context),
                    TraceId: context.TraceIdentifier,
                    ErrorCode: "AUTH_REFRESH_TOKEN_MISSING",
                    Message: "Refresh cookie was not present."),
                ct);

            return Error(context, 401, "AUTH_REFRESH_TOKEN_MISSING", "Oturum yenileme anahtarı bulunamadı.");
        }

        var result = await authService.RefreshAsync(refreshToken, GetIpAddress(context), GetDeviceName(context), ct);

        await TryAuditAsync(
            auditService,
            loggerFactory,
            new AuditEvent(
                AuditCategories.Security,
                result.Succeeded ? "AUTH_REFRESH_SUCCEEDED" : "AUTH_REFRESH_FAILED",
                result.Succeeded,
                result.Succeeded ? AuditSeverities.Info : AuditSeverities.Warning,
                result.User?.Id,
                result.User?.Username,
                GetIpAddress(context),
                GetDeviceName(context),
                context.TraceIdentifier,
                "USER",
                result.User?.Id.ToString(),
                result.ErrorCode,
                result.ErrorMessage),
            ct);

        if (!result.Succeeded || result.User is null || result.Tokens is null)
        {
            DeleteRefreshCookie(context, environment);
            return Error(context, 401, result.ErrorCode ?? "AUTH_REFRESH_TOKEN_INVALID", result.ErrorMessage ?? "Oturum yenilenemedi.");
        }

        SetRefreshCookie(context, environment, result.Tokens.RefreshToken, result.Tokens.RefreshTokenExpiresAt);
        return Results.Ok(new AuthResponse(
            result.User.Id,
            result.User.Username,
            result.User.Email,
            result.Tokens.AccessToken,
            result.Tokens.AccessTokenExpiresAt));
    }

    private static async Task<IResult> LogoutAsync(
        AuthService authService,
        AuditService auditService,
        ILoggerFactory loggerFactory,
        HttpContext context,
        IWebHostEnvironment environment,
        CancellationToken ct)
    {
        context.Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken);
        await authService.LogoutAsync(refreshToken, GetIpAddress(context), ct);
        DeleteRefreshCookie(context, environment);

        await TryAuditAsync(
            auditService,
            loggerFactory,
            new AuditEvent(
                AuditCategories.Security,
                "AUTH_LOGOUT",
                true,
                AuditSeverities.Info,
                IpAddress: GetIpAddress(context),
                UserAgent: GetDeviceName(context),
                TraceId: context.TraceIdentifier),
            ct);

        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAllAsync(
        ClaimsPrincipal principal,
        AuthService authService,
        AuditService auditService,
        ILoggerFactory loggerFactory,
        HttpContext context,
        IWebHostEnvironment environment,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
            return Error(context, 401, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");

        var succeeded = await authService.LogoutAllAsync(userId, GetIpAddress(context), ct);
        DeleteRefreshCookie(context, environment);

        await TryAuditAsync(
            auditService,
            loggerFactory,
            new AuditEvent(
                AuditCategories.Security,
                "AUTH_LOGOUT_ALL",
                succeeded,
                succeeded ? AuditSeverities.Info : AuditSeverities.Warning,
                userId,
                principal.FindFirst("unique_name")?.Value,
                GetIpAddress(context),
                GetDeviceName(context),
                context.TraceIdentifier,
                "USER",
                userId.ToString(),
                succeeded ? null : "AUTH_USER_NOT_FOUND",
                succeeded ? "All sessions invalidated." : "Session invalidation could not be completed."),
            ct);

        return succeeded
            ? Results.NoContent()
            : Error(context, 404, "AUTH_USER_NOT_FOUND", "Kullanıcı bulunamadı.");
    }

    private static async Task<IResult> MeAsync(
        ClaimsPrincipal principal,
        AccessControlService accessControlService,
        HttpContext context,
        CancellationToken ct)
    {
        var subject = principal.FindFirst("sub")?.Value;
        var username = principal.FindFirst("unique_name")?.Value;
        var email = principal.FindFirst("email")?.Value;
        var securityVersionRaw = principal.FindFirst("sv")?.Value;

        if (!Guid.TryParse(subject, out var userId)
            || string.IsNullOrWhiteSpace(username)
            || !int.TryParse(securityVersionRaw, out var securityVersion))
            return Error(context, 401, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");

        var snapshot = await accessControlService.GetSnapshotAsync(userId, ct);
        return Results.Ok(new MeResponse(
            userId,
            username,
            email,
            securityVersion,
            snapshot.Roles,
            snapshot.Permissions,
            snapshot.Scopes));
    }

    private static async Task TryAuditAsync(
        AuditService auditService,
        ILoggerFactory loggerFactory,
        AuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            await auditService.WriteAsync(auditEvent, cancellationToken);
        }
        catch (Exception exception)
        {
            loggerFactory
                .CreateLogger("SecurityAudit")
                .LogError(exception, "Security audit write failed for event {EventType}.", auditEvent.EventType);
        }
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out userId);

    private static IResult Error(HttpContext context, int statusCode, string code, string message) =>
        Results.Json(ApiErrorResponse.Create(code, message, context.TraceIdentifier), statusCode: statusCode);

    private static string? GetIpAddress(HttpContext context) => context.Connection.RemoteIpAddress?.ToString();
    private static string? GetDeviceName(HttpContext context) => context.Request.Headers["User-Agent"].ToString();

    private static void SetRefreshCookie(
        HttpContext context,
        IWebHostEnvironment environment,
        string refreshToken,
        DateTimeOffset expiresAt) =>
        context.Response.Cookies.Append(RefreshCookieName, refreshToken, CreateCookieOptions(environment, expiresAt));

    private static void DeleteRefreshCookie(HttpContext context, IWebHostEnvironment environment) =>
        context.Response.Cookies.Delete(RefreshCookieName, CreateCookieOptions(environment, DateTimeOffset.UnixEpoch));

    private static CookieOptions CreateCookieOptions(
        IWebHostEnvironment environment,
        DateTimeOffset expiresAt) => new()
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/api/v1/auth",
            Expires = expiresAt,
            IsEssential = true
        };
}
