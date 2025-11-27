using System.Globalization;
using ArcFaceSandbox.EmbeddingService;
using ArcFaceSandbox.UsersKernel.Infrastructure.Data;
using ArcFaceSandbox.UsersKernel.Infrastructure.Entities;
using ArcFaceSandbox.UsersKernel.Infrastructure.Models;
using ArcFaceSandbox.VectorStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArcFaceSandbox.UsersKernel.Infrastructure.Services;

/// <summary>
/// Coordinates persistence, embeddings, and vector-store synchronization for sandbox users.
/// </summary>
public sealed class SandboxUserService : ISandboxUserService
{
    private readonly SandboxUsersDbContext _context;
    private readonly IArcFaceEmbeddingService _embeddingService;
    private readonly ISandboxVectorStore _vectorStore;
    private readonly ILogger<SandboxUserService> _logger;

    public SandboxUserService(
        SandboxUsersDbContext context,
        IArcFaceEmbeddingService embeddingService,
        ISandboxVectorStore vectorStore,
        ILogger<SandboxUserService> logger)
    {
        _context = context;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public async Task<SandboxUser> UpsertAsync(UserUpsertCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.FaceImageBase64))
        {
            throw new ArgumentException("Face image payload is required for upsert operations.", nameof(command));
        }

        var embedding = await GenerateEmbeddingAsync(command.FaceImageBase64, cancellationToken).ConfigureAwait(false);
        var normalizedEmail = command.Email.Trim();
        var now = DateTime.UtcNow;

        var existing = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            existing = new SandboxUser
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                DisplayName = command.DisplayName.Trim(),
                Role = command.Role,
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true
            };

            await _context.Users.AddAsync(existing, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            existing.DisplayName = command.DisplayName.Trim();
            existing.IsActive = true;
            existing.DeletedAt = null;
            existing.UpdatedAt = now;

            if (existing.Role == SandboxUserRole.User && command.Role == SandboxUserRole.Admin)
            {
                existing.Role = SandboxUserRole.Admin;
            }
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await UpsertVectorAsync(existing, embedding, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Upserted sandbox user {UserId} ({Email})", existing.Id, existing.Email);
        return existing;
    }

    public async Task<SandboxUser?> UpdateAsync(Guid id, UserUpdateCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var changed = false;

        if (!string.IsNullOrWhiteSpace(command.DisplayName) && !string.Equals(user.DisplayName, command.DisplayName, StringComparison.Ordinal))
        {
            user.DisplayName = command.DisplayName.Trim();
            changed = true;
        }

        if (command.Role is SandboxUserRole.Admin && user.Role == SandboxUserRole.User)
        {
            user.Role = SandboxUserRole.Admin;
            changed = true;
        }

        if (command.IsActive.HasValue && command.IsActive.Value != user.IsActive)
        {
            user.IsActive = command.IsActive.Value;
            user.DeletedAt = user.IsActive ? null : now;
            changed = true;
        }

        ReadOnlyMemory<float>? embedding = null;
        if (!string.IsNullOrWhiteSpace(command.FaceImageBase64))
        {
            embedding = await GenerateEmbeddingAsync(command.FaceImageBase64, cancellationToken).ConfigureAwait(false);
            changed = true;
        }

        if (!changed)
        {
            return user;
        }

        user.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (!user.IsActive)
        {
            await _vectorStore.DownsertAsync(GetDocumentId(user.Id), cancellationToken).ConfigureAwait(false);
            return user;
        }

        if (embedding.HasValue)
        {
            await UpsertVectorAsync(user, embedding.Value, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await RefreshVectorMetadataAsync(user, cancellationToken).ConfigureAwait(false);
        }

        return user;
    }

    public async Task<bool> DownsertAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return false;
        }

        if (!user.IsActive && user.DeletedAt is not null)
        {
            return true;
        }

        var now = DateTime.UtcNow;
        user.IsActive = false;
        user.DeletedAt = now;
        user.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _vectorStore.DownsertAsync(GetDocumentId(user.Id), cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<SandboxUser?> RecordLoginAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        user.LastLoginAt = now;
        user.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return user;
    }

    public async Task<SandboxUser?> GetByIdAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = includeInactive ? _context.Users.IgnoreQueryFilters() : _context.Users;
        return await query.FirstOrDefaultAsync(u => u.Id == id, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string faceImageBase64, CancellationToken cancellationToken)
    {
        var payload = faceImageBase64.Trim();
        var commaIndex = payload.IndexOf(',', StringComparison.Ordinal);
        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0)
        {
            payload = payload[(commaIndex + 1)..];
        }

        byte[] buffer;
        try
        {
            buffer = Convert.FromBase64String(payload);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Face image payload must be base64 encoded.", nameof(faceImageBase64), ex);
        }

        await using var stream = new MemoryStream(buffer, writable: false);
        var embedding = await _embeddingService.GenerateAsync(stream, cancellationToken).ConfigureAwait(false);
        return embedding.ToArray();
    }

    private async Task UpsertVectorAsync(SandboxUser user, ReadOnlyMemory<float> embedding, CancellationToken cancellationToken)
    {
        var document = new SandboxVectorDocument
        {
            Id = GetDocumentId(user.Id),
            Content = user.DisplayName,
            Embedding = embedding.ToArray(),
            Metadata = BuildMetadata(user),
            IsDeleted = !user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            DeletedAt = user.DeletedAt
        };

        await _vectorStore.UpsertAsync(document, cancellationToken).ConfigureAwait(false);
    }

    private async Task RefreshVectorMetadataAsync(SandboxUser user, CancellationToken cancellationToken)
    {
        var existing = await _vectorStore.GetAsync(GetDocumentId(user.Id), cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            _logger.LogWarning("Vector document missing for sandbox user {UserId}", user.Id);
            return;
        }

        var updated = existing with
        {
            Content = user.DisplayName,
            Metadata = BuildMetadata(user),
            IsDeleted = !user.IsActive,
            DeletedAt = user.DeletedAt,
            UpdatedAt = user.UpdatedAt
        };

        await _vectorStore.UpsertAsync(updated, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyDictionary<string, string> BuildMetadata(SandboxUser user)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["email"] = user.Email,
            ["role"] = user.Role.ToString(),
            ["is_active"] = user.IsActive.ToString(CultureInfo.InvariantCulture)
        };
    }

    private static string GetDocumentId(Guid id) => id.ToString("N", CultureInfo.InvariantCulture);
}
