using Aspire_Full.Shared.Models;

namespace Aspire_Full.Gateway.Models;

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
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the user was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// When the user was soft deleted (null if active).
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Last login timestamp.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
}
