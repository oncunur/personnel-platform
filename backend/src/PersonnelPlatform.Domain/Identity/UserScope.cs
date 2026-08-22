using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Identity;

public sealed class UserScope : Entity
{
    private UserScope() { }

    private UserScope(Guid userId, string scopeType, Guid? scopeId, DateTimeOffset validFrom, DateTimeOffset? validUntil, DateTimeOffset createdAt, Guid? createdBy)
    {
        UserId = userId;
        ScopeType = scopeType;
        ScopeId = scopeId;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        IsActive = true;
    }

    public Guid UserId { get; private set; }
    public string ScopeType { get; private set; } = string.Empty;
    public Guid? ScopeId { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset? ValidUntil { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }

    public static UserScope Create(Guid userId, string scopeType, Guid? scopeId, DateTimeOffset validFrom, DateTimeOffset? validUntil, DateTimeOffset createdAt, Guid? createdBy)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User id must not be empty.", nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeType);
        if (validUntil is not null && validUntil <= validFrom)
            throw new ArgumentException("Scope validity end must be after start.", nameof(validUntil));

        return new UserScope(userId, scopeType.Trim().ToUpperInvariant(), scopeId, validFrom, validUntil, createdAt, createdBy);
    }
}
