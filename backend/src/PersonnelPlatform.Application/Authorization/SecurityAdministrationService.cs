using PersonnelPlatform.Application.Identity;
using PersonnelPlatform.Domain.Identity;

namespace PersonnelPlatform.Application.Authorization;

public sealed class SecurityAdministrationService(IIdentityRepository identityRepository, IAuthorizationRepository authorizationRepository, IPasswordHasher passwordHasher, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<SecurityUserSummary>> ListUsersAsync(CancellationToken cancellationToken)
    {
        var users = await identityRepository.ListUsersAsync(cancellationToken);
        return users.Select(ToUserSummary).OrderBy(x => x.Username, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<SecurityResult<SecurityUserSummary>> CreateUserAsync(CreateSecurityUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > 100)
            return SecurityResult<SecurityUserSummary>.Failure("USER_USERNAME_INVALID", "Kullanıcı adı zorunludur ve en fazla 100 karakter olabilir.");
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 12 || request.Password.Length > 512)
            return SecurityResult<SecurityUserSummary>.Failure("USER_PASSWORD_INVALID", "Parola 12-512 karakter arasında olmalıdır.");
        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email.Length > 320)
            return SecurityResult<SecurityUserSummary>.Failure("USER_EMAIL_INVALID", "E-posta en fazla 320 karakter olabilir.");

        var normalizedUsername = IdentityNormalizer.NormalizeUsername(request.Username);
        if (await identityRepository.FindUserByNormalizedUsernameAsync(normalizedUsername, cancellationToken) is not null)
            return SecurityResult<SecurityUserSummary>.Failure("USER_USERNAME_ALREADY_EXISTS", "Bu kullanıcı adı zaten kullanılıyor.");

        var normalizedEmail = IdentityNormalizer.NormalizeEmail(request.Email);
        if (normalizedEmail is not null && await identityRepository.FindUserByNormalizedEmailAsync(normalizedEmail, cancellationToken) is not null)
            return SecurityResult<SecurityUserSummary>.Failure("USER_EMAIL_ALREADY_EXISTS", "Bu e-posta zaten kullanılıyor.");

        var now = timeProvider.GetUtcNow();
        var user = User.Create(request.Username, normalizedUsername, request.Email, normalizedEmail, passwordHasher.Hash(request.Password), now);
        identityRepository.AddUser(user);
        await identityRepository.SaveChangesAsync(cancellationToken);
        return SecurityResult<SecurityUserSummary>.Success(ToUserSummary(user));
    }

    public Task<IReadOnlyList<Role>> ListRolesAsync(CancellationToken cancellationToken) => authorizationRepository.ListRolesAsync(cancellationToken);
    public Task<IReadOnlyList<Permission>> ListPermissionsAsync(CancellationToken cancellationToken) => authorizationRepository.ListPermissionsAsync(cancellationToken);

    public async Task<SecurityResult<RoleSummary>> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length > 100 || string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 150)
            return SecurityResult<RoleSummary>.Failure("ROLE_DATA_INVALID", "Rol kodu ve adı zorunludur.");
        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (await authorizationRepository.FindRoleByCodeAsync(normalizedCode, cancellationToken) is not null)
            return SecurityResult<RoleSummary>.Failure("ROLE_CODE_ALREADY_EXISTS", "Bu rol kodu zaten kullanılıyor.");

        var role = Role.Create(normalizedCode, request.Name, request.Description, timeProvider.GetUtcNow());
        authorizationRepository.AddRole(role);
        await authorizationRepository.SaveChangesAsync(cancellationToken);
        return SecurityResult<RoleSummary>.Success(new RoleSummary(role.Id, role.Code, role.Name));
    }

    public async Task<SecurityResult<RoleSummary>> SetRolePermissionsAsync(Guid roleId, IReadOnlyCollection<Guid> permissionIds, Guid? actorUserId, CancellationToken cancellationToken)
    {
        var role = await authorizationRepository.FindRoleByIdAsync(roleId, cancellationToken);
        if (role is null) return SecurityResult<RoleSummary>.Failure("ROLE_NOT_FOUND", "Rol bulunamadı.");
        var valid = (await authorizationRepository.ListPermissionsAsync(cancellationToken)).Select(x => x.Id).ToHashSet();
        if (permissionIds.Any(id => !valid.Contains(id))) return SecurityResult<RoleSummary>.Failure("PERMISSION_NOT_FOUND", "Seçilen yetkilerden biri bulunamadı.");
        await authorizationRepository.ReplaceRolePermissionsAsync(roleId, permissionIds.Distinct().ToArray(), actorUserId, timeProvider.GetUtcNow(), cancellationToken);
        await authorizationRepository.SaveChangesAsync(cancellationToken);
        return SecurityResult<RoleSummary>.Success(new RoleSummary(role.Id, role.Code, role.Name));
    }

    public async Task<SecurityResult<AuthorizationSnapshot>> SetUserRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, Guid? actorUserId, CancellationToken cancellationToken)
    {
        if (await identityRepository.FindUserByIdAsync(userId, cancellationToken) is null) return SecurityResult<AuthorizationSnapshot>.Failure("USER_NOT_FOUND", "Kullanıcı bulunamadı.");
        var valid = (await authorizationRepository.ListRolesAsync(cancellationToken)).Where(x => x.IsActive).Select(x => x.Id).ToHashSet();
        if (roleIds.Any(id => !valid.Contains(id))) return SecurityResult<AuthorizationSnapshot>.Failure("ROLE_NOT_FOUND", "Seçilen rollerden biri bulunamadı veya pasif.");
        var now = timeProvider.GetUtcNow();
        await authorizationRepository.ReplaceUserRolesAsync(userId, roleIds.Distinct().ToArray(), actorUserId, now, cancellationToken);
        await authorizationRepository.SaveChangesAsync(cancellationToken);
        return SecurityResult<AuthorizationSnapshot>.Success(await authorizationRepository.GetSnapshotAsync(userId, now, cancellationToken));
    }

    public async Task<SecurityResult<AuthorizationSnapshot>> SetUserScopesAsync(Guid userId, IReadOnlyCollection<UserScopeInput> scopeInputs, Guid? actorUserId, CancellationToken cancellationToken)
    {
        if (await identityRepository.FindUserByIdAsync(userId, cancellationToken) is null) return SecurityResult<AuthorizationSnapshot>.Failure("USER_NOT_FOUND", "Kullanıcı bulunamadı.");
        var now = timeProvider.GetUtcNow();
        var scopes = new List<UserScope>();
        foreach (var input in scopeInputs)
        {
            if (string.IsNullOrWhiteSpace(input.ScopeType)) return SecurityResult<AuthorizationSnapshot>.Failure("SCOPE_INVALID", "Scope tipi zorunludur.");
            var scopeType = input.ScopeType.Trim().ToUpperInvariant();
            if (scopeType is not (ScopeTypes.Global or ScopeTypes.Company)) return SecurityResult<AuthorizationSnapshot>.Failure("SCOPE_TYPE_NOT_SUPPORTED", "Sprint 1'de yalnız GLOBAL ve COMPANY scope atanabilir.");
            if (scopeType == ScopeTypes.Global && input.ScopeId is not null) return SecurityResult<AuthorizationSnapshot>.Failure("SCOPE_INVALID", "GLOBAL scope için scopeId boş olmalıdır.");
            if (scopeType == ScopeTypes.Company && input.ScopeId is null) return SecurityResult<AuthorizationSnapshot>.Failure("SCOPE_INVALID", "COMPANY scope için scopeId zorunludur.");
            var validFrom = input.ValidFrom ?? now;
            if (input.ValidUntil is not null && input.ValidUntil <= validFrom) return SecurityResult<AuthorizationSnapshot>.Failure("SCOPE_DATE_INVALID", "Scope bitiş tarihi başlangıçtan sonra olmalıdır.");
            scopes.Add(UserScope.Create(userId, scopeType, input.ScopeId, validFrom, input.ValidUntil, now, actorUserId));
        }
        await authorizationRepository.ReplaceUserScopesAsync(userId, scopes, cancellationToken);
        await authorizationRepository.SaveChangesAsync(cancellationToken);
        return SecurityResult<AuthorizationSnapshot>.Success(await authorizationRepository.GetSnapshotAsync(userId, now, cancellationToken));
    }

    public async Task<SecurityResult<SecurityUserSummary>> SetUserActiveAsync(Guid userId, bool active, CancellationToken cancellationToken)
    {
        var user = await identityRepository.FindUserByIdAsync(userId, cancellationToken);
        if (user is null) return SecurityResult<SecurityUserSummary>.Failure("USER_NOT_FOUND", "Kullanıcı bulunamadı.");
        var now = timeProvider.GetUtcNow();
        if (active) user.Activate(now); else user.Deactivate(now);
        await identityRepository.SaveChangesAsync(cancellationToken);
        return SecurityResult<SecurityUserSummary>.Success(ToUserSummary(user));
    }

    private static SecurityUserSummary ToUserSummary(User user) => new(user.Id, user.Username, user.Email, user.IsActive, user.LastLoginAt, user.SecurityVersion);
}
