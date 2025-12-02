using ArcFaceSandbox.UsersKernel.Infrastructure.Entities;

namespace ArcFaceSandbox.UsersKernel.Infrastructure.Models;

/// <summary>
/// Command used to upsert (create or reactivate) a sandbox user.
/// </summary>
public sealed record UserUpsertCommand(
    string Email,
    string DisplayName,
    SandboxUserRole Role,
    string FaceImageBase64);

/// <summary>
/// Command used to update user metadata or embeddings.
/// </summary>
public sealed record UserUpdateCommand(
    string? DisplayName,
    SandboxUserRole? Role,
    bool? IsActive,
    string? FaceImageBase64);
