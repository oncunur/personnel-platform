namespace PersonnelPlatform.Application.Authorization;

public sealed record SecurityUserSummary(Guid Id, string Username, string? Email, bool IsActive, DateTimeOffset? LastLoginAt, int SecurityVersion);
public sealed record CreateSecurityUserRequest(string Username, string? Email, string Password);
public sealed record CreateRoleRequest(string Code, string Name, string? Description);
public sealed record UserScopeInput(string ScopeType, Guid? ScopeId, DateTimeOffset? ValidFrom, DateTimeOffset? ValidUntil);

public sealed record SecurityResult<T>(bool Succeeded, T? Value, string? ErrorCode, string? ErrorMessage) where T : class
{
    public static SecurityResult<T> Success(T value) => new(true, value, null, null);
    public static SecurityResult<T> Failure(string code, string message) => new(false, null, code, message);
}
