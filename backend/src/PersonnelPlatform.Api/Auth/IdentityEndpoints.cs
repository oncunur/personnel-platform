using System.Security.Claims;
using PersonnelPlatform.Api.Contracts;
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
        group.MapGet("/me", Me).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        AuthService authService,
        HttpContext context,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            return Error(context, StatusCodes.Status400BadRequest, "AUTH_VALIDATION_ERROR", "Kullanıcı adı ve parola zorunludur.");
        }

        if (request.Username.Length > 100 || request.Password.Length > 512)
        {
            return Error(context, StatusCodes.Status400BadRequest, "AUTH_VALIDATION_ERROR", "Giriş bilgileri izin verilen uzunluğu aşıyor.");
        }

        var result = await authService.LoginAsync(
            request.Username,
            request.Password,
            GetIpAddress(context),
            GetDeviceName(context),
            cancellationToken);

        if (!result.Succeeded || result.User is null || result.Tokens is null)
        {
            return Error(
                context,
                StatusCodes.Status401Unauthorized,
                result.ErrorCode ?? "AUTH_INVALID_CREDENTIALS",
                result.ErrorMessage ?? "Kimlik doğrulama başarısız.");
        }

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
        HttpContext context,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken))
        {
            return Error(context, StatusCodes.Status401Unauthorized, "AUTH_REFRESH_TOKEN_MISSING", "Oturum yenileme anahtarı bulunamadı.");
        }

        var result = await authService.RefreshAsync(
            refreshToken,
            GetIpAddress(context),
            GetDeviceName(context),
            cancellationToken);

        if (!result.Succeeded || result.User is null || result.Tokens is null)
        {
            DeleteRefreshCookie(context, environment);
            return Error(
                context,
                StatusCodes.Status401Unauthorized,
                result.ErrorCode ?? "AUTH_REFRESH_TOKEN_INVALID",
                result.ErrorMessage ?? "Oturum yenilenemedi.");
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
        HttpContext context,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        context.Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken);
        await authService.LogoutAsync(refreshToken, GetIpAddress(context), cancellationToken);
        DeleteRefreshCookie(context, environment);
        return Results.NoContent();
    }

    private static IResult Me(ClaimsPrincipal principal, HttpContext context)
    {
        var subject = principal.FindFirst("sub")?.Value;
        var username = principal.FindFirst("unique_name")?.Value;
        var email = principal.FindFirst("email")?.Value;
        var securityVersionRaw = principal.FindFirst("sv")?.Value;

        if (!Guid.TryParse(subject, out var userId)
            || string.IsNullOrWhiteSpace(username)
            || !int.TryParse(securityVersionRaw, out var securityVersion))
        {
            return Error(context, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");
        }

        return Results.Ok(new MeResponse(userId, username, email, securityVersion));
    }

    private static IResult Error(HttpContext context, int statusCode, string code, string message) =>
        Results.Json(
            ApiErrorResponse.Create(code, message, context.TraceIdentifier),
            statusCode: statusCode);

    private static string? GetIpAddress(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString();

    private static string? GetDeviceName(HttpContext context) =>
        context.Request.Headers["User-Agent"].ToString();

    private static void SetRefreshCookie(
        HttpContext context,
        IWebHostEnvironment environment,
        string refreshToken,
        DateTimeOffset expiresAt)
    {
        context.Response.Cookies.Append(
            RefreshCookieName,
            refreshToken,
            CreateCookieOptions(environment, expiresAt));
    }

    private static void DeleteRefreshCookie(HttpContext context, IWebHostEnvironment environment)
    {
        var options = CreateCookieOptions(environment, DateTimeOffset.UnixEpoch);
        context.Response.Cookies.Delete(RefreshCookieName, options);
    }

    private static CookieOptions CreateCookieOptions(IWebHostEnvironment environment, DateTimeOffset expiresAt) =>
        new()
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/api/v1/auth",
            Expires = expiresAt,
            IsEssential = true
        };
}
