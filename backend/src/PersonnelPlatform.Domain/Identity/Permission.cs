using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Identity;

public sealed class Permission : Entity
{
    private Permission() { }

    private Permission(string code, string name, string module, string? description)
    {
        Code = code;
        Name = name;
        Module = module;
        Description = description;
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Module { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }

    public static Permission Create(string code, string name, string module, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        return new Permission(code.Trim().ToLowerInvariant(), name.Trim(), module.Trim(), description?.Trim());
    }
}
