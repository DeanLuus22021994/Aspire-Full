namespace Aspire_Full.Api.Models;

/// <summary>
/// User entity representing both standard users and administrators.
/// Supports soft delete (downsert) and upsert patterns.
/// </summary>
public class User
{
    public int Id { get; set; }

    /// <summary>
    /// Unique email address for the user.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// Display name shown in the UI.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// User's role: User or Admin.
    /// </summary>
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>
    /// Whether the user account is active (false = soft deleted/downserted).
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When the user was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the user was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the user was soft deleted (null if active).
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Last login timestamp.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Navigation property: Items created by this user.
    /// </summary>
    public ICollection<Item> Items { get; set; } = new List<Item>();
}

/// <summary>
/// User roles for authorization.
/// </summary>
public enum UserRole
{
    User = 0,
    Admin = 1
}

#region DTOs

/// <summary>
/// DTO for creating a new user (upsert operation).
/// </summary>
public class CreateUserDto
{
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
}

/// <summary>
/// DTO for updating an existing user.
/// </summary>
public class UpdateUserDto
{
    public string? DisplayName { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// DTO for user responses (excludes sensitive data).
/// </summary>
public class UserResponseDto
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public static UserResponseDto FromUser(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        Role = user.Role,
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt,
        LastLoginAt = user.LastLoginAt
    };
}

/// <summary>
/// DTO for admin user list with additional metadata.
/// </summary>
public class AdminUserResponseDto : UserResponseDto
{
    public DateTime? DeletedAt { get; set; }
    public int ItemCount { get; set; }

    public static new AdminUserResponseDto FromUser(User user) => new()
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

#endregion
