using System.IO.Abstractions;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aspire_Full.Shared.IO;

/// <summary>
/// Configuration for secure shared storage access.
/// </summary>
public sealed class SharedStorageOptions
{
    /// <summary>
    /// The single allowed host mount path. This is the ONLY path that can access the host filesystem.
    /// Windows: C:\SHARED, Linux (container): /shared
    /// </summary>
    public string HostMountPath { get; set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? @"C:\SHARED"
        : "/shared";

    /// <summary>
    /// Maximum allowed path depth within the shared directory.
    /// Prevents excessive nesting that could indicate path manipulation.
    /// </summary>
    public int MaxPathDepth { get; set; } = 10;

    /// <summary>
    /// Subdirectories that are created and managed within the shared mount.
    /// </summary>
    public SharedSubdirectories Subdirectories { get; set; } = new();

    /// <summary>
    /// Enable strict validation that blocks all symlinks.
    /// </summary>
    public bool BlockSymlinks { get; set; } = true;

    /// <summary>
    /// Enable read-only mode for defensive access.
    /// </summary>
    public bool ReadOnlyMode { get; set; } = false;
}

/// <summary>
/// Well-known subdirectories within the shared mount.
/// </summary>
public sealed class SharedSubdirectories
{
    public string Models { get; set; } = "models";
    public string VectorStore { get; set; } = "qdrant";
    public string Cache { get; set; } = "cache";
    public string Logs { get; set; } = "logs";
    public string Temp { get; set; } = "tmp";
}

/// <summary>
/// Defensive path validation service that provides jail-protected access to the shared storage mount.
/// Implements strict path sanitization to protect both host and container.
/// </summary>
public interface ISecurePathService
{
    /// <summary>Gets the resolved base path for the shared mount.</summary>
    string BasePath { get; }

    /// <summary>Gets the path to the models directory.</summary>
    string ModelsPath { get; }

    /// <summary>Gets the path to the vector store directory.</summary>
    string VectorStorePath { get; }

    /// <summary>Gets the path to the cache directory.</summary>
    string CachePath { get; }

    /// <summary>
    /// Validates and resolves a path within the shared mount.
    /// Throws if the path escapes the jail.
    /// </summary>
    /// <param name="relativePath">The relative path within the shared mount.</param>
    /// <returns>The fully resolved, validated absolute path.</returns>
    string ResolvePath(string relativePath);

    /// <summary>
    /// Attempts to validate and resolve a path within the shared mount.
    /// </summary>
    /// <param name="relativePath">The relative path within the shared mount.</param>
    /// <param name="resolvedPath">The resolved path if valid, null otherwise.</param>
    /// <returns>True if the path is valid and within the jail.</returns>
    bool TryResolvePath(string relativePath, out string? resolvedPath);

    /// <summary>
    /// Validates that an absolute path is within the shared mount.
    /// </summary>
    /// <param name="absolutePath">The absolute path to validate.</param>
    /// <returns>True if the path is within the shared mount jail.</returns>
    bool IsPathWithinJail(string absolutePath);

    /// <summary>
    /// Ensures all subdirectories exist within the shared mount.
    /// </summary>
    void EnsureDirectoriesExist();
}

/// <summary>
/// Implementation of secure path validation with defensive programming.
/// </summary>
public sealed class SecurePathService : ISecurePathService
{
    private readonly SharedStorageOptions _options;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<SecurePathService> _logger;
    private readonly string _normalizedBasePath;

    // Dangerous path components that indicate path traversal attempts
    private static readonly string[] DangerousPatterns =
    [
        "..",
        "..\\",
        "../",
        "%2e%2e",
        "%252e%252e",
        "..%c0%af",
        "..%c1%9c",
        "\\\\",
        "//",
        "::",
        "\0"
    ];

    public SecurePathService(
        IOptions<SharedStorageOptions> options,
        IFileSystem fileSystem,
        ILogger<SecurePathService> logger)
    {
        _options = options.Value;
        _fileSystem = fileSystem;
        _logger = logger;

        // Normalize and validate the base path at construction time
        _normalizedBasePath = NormalizeAndValidateBasePath(_options.HostMountPath);
    }

    /// <inheritdoc />
    public string BasePath => _normalizedBasePath;

    /// <inheritdoc />
    public string ModelsPath => ResolvePath(_options.Subdirectories.Models);

    /// <inheritdoc />
    public string VectorStorePath => ResolvePath(_options.Subdirectories.VectorStore);

    /// <inheritdoc />
    public string CachePath => ResolvePath(_options.Subdirectories.Cache);

    /// <inheritdoc />
    public string ResolvePath(string relativePath)
    {
        if (!TryResolvePath(relativePath, out var resolvedPath))
        {
            throw new SecurityException($"Path '{relativePath}' escapes the shared mount jail or contains invalid characters.");
        }

        return resolvedPath!;
    }

    /// <inheritdoc />
    public bool TryResolvePath(string relativePath, out string? resolvedPath)
    {
        resolvedPath = null;

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            _logger.LogWarning("Attempted to resolve empty or null path");
            return false;
        }

        // Check for dangerous patterns before any path manipulation
        if (ContainsDangerousPatterns(relativePath))
        {
            _logger.LogWarning("Path contains dangerous patterns: {Path}", SanitizeForLogging(relativePath));
            return false;
        }

        try
        {
            // Combine with base path
            var combinedPath = _fileSystem.Path.Combine(_normalizedBasePath, relativePath);

            // Get the full, canonicalized path (resolves all . and .. components)
            var fullPath = _fileSystem.Path.GetFullPath(combinedPath);

            // Normalize for comparison
            var normalizedFullPath = NormalizePath(fullPath);

            // Verify the resolved path is still within the jail
            if (!normalizedFullPath.StartsWith(_normalizedBasePath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Path escaped jail: input='{Input}' resolved to '{Resolved}' which is outside '{Jail}'",
                    SanitizeForLogging(relativePath),
                    SanitizeForLogging(normalizedFullPath),
                    _normalizedBasePath);
                return false;
            }

            // Check path depth
            var depth = GetPathDepth(normalizedFullPath, _normalizedBasePath);
            if (depth > _options.MaxPathDepth)
            {
                _logger.LogWarning("Path exceeds maximum depth ({MaxDepth}): {Path}", _options.MaxPathDepth, SanitizeForLogging(relativePath));
                return false;
            }

            // Check for symlinks if configured
            if (_options.BlockSymlinks && ContainsSymlink(normalizedFullPath))
            {
                _logger.LogWarning("Path contains symlink which is blocked: {Path}", SanitizeForLogging(relativePath));
                return false;
            }

            resolvedPath = normalizedFullPath;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Invalid path format: {Path}", SanitizeForLogging(relativePath));
            return false;
        }
    }

    /// <inheritdoc />
    public bool IsPathWithinJail(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return false;

        try
        {
            var normalizedPath = NormalizePath(_fileSystem.Path.GetFullPath(absolutePath));
            return normalizedPath.StartsWith(_normalizedBasePath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public void EnsureDirectoriesExist()
    {
        var directories = new[]
        {
            ModelsPath,
            VectorStorePath,
            CachePath,
            ResolvePath(_options.Subdirectories.Logs),
            ResolvePath(_options.Subdirectories.Temp)
        };

        foreach (var dir in directories)
        {
            if (!_fileSystem.Directory.Exists(dir))
            {
                _fileSystem.Directory.CreateDirectory(dir);
                _logger.LogInformation("Created shared directory: {Path}", dir);
            }
        }
    }

    private string NormalizeAndValidateBasePath(string basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            throw new ArgumentException("Base path cannot be null or empty", nameof(basePath));
        }

        // Get canonical path
        var normalized = NormalizePath(_fileSystem.Path.GetFullPath(basePath));

        // Ensure it's not a root drive/path that would be too permissive
        if (normalized.Length <= 3) // e.g., "C:\" or "/"
        {
            throw new SecurityException($"Base path '{basePath}' is too permissive (root or near-root path).");
        }

        _logger.LogInformation("Secure path service initialized with base path: {BasePath}", normalized);
        return normalized;
    }

    private static string NormalizePath(string path)
    {
        // Normalize separators and ensure consistent trailing separator handling
        var normalized = path.Replace('/', Path.DirectorySeparatorChar)
                            .Replace('\\', Path.DirectorySeparatorChar)
                            .TrimEnd(Path.DirectorySeparatorChar);

        // Add trailing separator for consistent prefix matching
        return normalized + Path.DirectorySeparatorChar;
    }

    private static bool ContainsDangerousPatterns(string path)
    {
        var lowerPath = path.ToLowerInvariant();
        return DangerousPatterns.Any(pattern => lowerPath.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetPathDepth(string fullPath, string basePath)
    {
        var relativePart = fullPath[basePath.Length..];
        return relativePart.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private bool ContainsSymlink(string path)
    {
        try
        {
            // Check each component of the path for symlinks
            var current = _normalizedBasePath.TrimEnd(Path.DirectorySeparatorChar);
            var components = path[_normalizedBasePath.Length..]
                .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

            foreach (var component in components)
            {
                current = _fileSystem.Path.Combine(current, component);

                if (_fileSystem.File.Exists(current) || _fileSystem.Directory.Exists(current))
                {
                    var attributes = _fileSystem.File.GetAttributes(current);
                    if ((attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch
        {
            // If we can't check, assume it's unsafe
            return true;
        }
    }

    private static string SanitizeForLogging(string input)
    {
        // Remove any control characters and limit length for safe logging
        const int maxLength = 200;
        var sanitized = new string(input.Where(c => !char.IsControl(c)).ToArray());
        return sanitized.Length > maxLength ? sanitized[..maxLength] + "..." : sanitized;
    }
}

/// <summary>
/// Security exception for path validation failures.
/// </summary>
public class SecurityException : Exception
{
    public SecurityException(string message) : base(message) { }
    public SecurityException(string message, Exception inner) : base(message, inner) { }
}
