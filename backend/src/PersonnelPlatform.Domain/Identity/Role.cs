using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Identity;

public sealed class Role : AuditableEntity
{
    private Role()
    {
    }

    private Role(string code, string name, string? description, DateTimeOffset createdAt)
    {
        Code = code;
        Name = name;
        Description = description;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }

    public static Role Create(string code, string name, string? description, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Role(code.Trim().ToUpperInvariant(), name.Trim(), description?.Trim(), createdAt);
    }

    public void Update(string name, string? description, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedAt = now;
        Version++;
    }

    public void SetActive(bool active, DateTimeOffset now)
    {
        if (IsActive == active) return;
        IsActive = active;
        UpdatedAt = now;
        Version++;
    }
}
