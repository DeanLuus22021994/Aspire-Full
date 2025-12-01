using Aspire_Full.Shared.Models;

namespace Aspire_Full.Api.Models;

public sealed record AdminUserResponseDto
{
    public int Id { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public UserRole Role { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public DateTime? DeletedAt { get; init; }
    public int ItemCount { get; init; }

    public static AdminUserResponseDto FromUser(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        Role = user.Role,
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt,
        LastLoginAt = user.LastLoginAt,
        DeletedAt = user.DeletedAt,
        ItemCount = user.Items.Count
    };
}
