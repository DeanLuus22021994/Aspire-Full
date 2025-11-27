using System.ComponentModel.DataAnnotations;
using ArcFaceSandbox.UsersKernel.Infrastructure.Entities;

namespace ArcFaceSandbox.UsersKernel.Api.Contracts;

public sealed class UpsertSandboxUserRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public SandboxUserRole Role { get; set; } = SandboxUserRole.User;

    [Required]
    public string FaceImageBase64 { get; set; } = string.Empty;
}

public sealed class UpdateSandboxUserRequest
{
    [MaxLength(100)]
    public string? DisplayName { get; set; }

    public SandboxUserRole? Role { get; set; }

    public bool? IsActive { get; set; }

    public string? FaceImageBase64 { get; set; }
}

public sealed record SandboxUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    SandboxUserRole Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? DeletedAt,
    DateTime? LastLoginAt)
{
    public static SandboxUserResponse FromEntity(ArcFaceSandbox.UsersKernel.Infrastructure.Entities.SandboxUser user) => new(
        user.Id,
        user.Email,
        user.DisplayName,
        user.Role,
        user.IsActive,
        user.CreatedAt,
        user.UpdatedAt,
        user.DeletedAt,
        user.LastLoginAt);
}
