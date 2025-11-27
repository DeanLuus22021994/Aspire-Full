namespace ArcFaceSandbox.UsersKernel.Infrastructure.Entities;

/// <summary>
/// Sandbox-specific user entity mirroring production soft-delete semantics.
/// </summary>
public class SandboxUser
{
    public Guid Id { get; set; }

    public required string Email { get; set; }

    public required string DisplayName { get; set; }

    public SandboxUserRole Role { get; set; } = SandboxUserRole.User;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }
}

/// <summary>
/// Roles supported by the sandbox Users kernel.
/// </summary>
public enum SandboxUserRole
{
    User = 0,
    Admin = 1
}
