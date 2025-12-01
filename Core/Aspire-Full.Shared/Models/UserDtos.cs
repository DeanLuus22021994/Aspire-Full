using System.Text.Json.Serialization;

namespace Aspire_Full.Shared.Models;

[JsonConverter(typeof(JsonStringEnumConverter<UserRole>))]
public enum UserRole
{
    User = 0,
    Admin = 1
}

public sealed record CreateUser
{
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public UserRole Role { get; init; } = UserRole.User;
}

public sealed record UpdateUser
{
    public string? DisplayName { get; init; }
    public bool? IsActive { get; init; }
}

public sealed record User
{
    public int Id { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public UserRole Role { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
}
