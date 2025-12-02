using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Aspire_Full.VectorStore;

/// <summary>
/// GPU-friendly throw helper that keeps hot paths clean for branch prediction.
/// Methods are marked to prevent inlining of exception throwing into performance-critical code.
/// </summary>
internal static class ThrowHelper
{
    /// <summary>
    /// Validates that the string ID is a valid GUID format.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guid ValidateAndParseGuid(string? id, [CallerArgumentExpression(nameof(id))] string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            ThrowArgumentNullOrWhiteSpace(paramName);
        }

        if (!Guid.TryParse(id, out var guid))
        {
            ThrowInvalidGuidFormat(id, paramName);
        }

        return guid;
    }

    /// <summary>
    /// Validates vector dimension matches expected size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateVectorDimension(int actualSize, int expectedSize, [CallerArgumentExpression(nameof(actualSize))] string? paramName = null)
    {
        if (actualSize != expectedSize)
        {
            ThrowVectorDimensionMismatch(actualSize, expectedSize, paramName);
        }
    }

    /// <summary>
    /// Validates that an object is not null.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull<T>([NotNull] T? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null) where T : class
    {
        if (argument is null)
        {
            ThrowArgumentNull(paramName);
        }
    }

    /// <summary>
    /// Validates topK is within valid range.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateTopK(int topK, int maxValue = 10000)
    {
        if (topK <= 0 || topK > maxValue)
        {
            ThrowTopKOutOfRange(topK, maxValue);
        }
    }

    #region Throw Methods - Never inlined to keep hot path clean

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentNullOrWhiteSpace(string? paramName)
    {
        throw new ArgumentException("Value cannot be null or whitespace.", paramName);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidGuidFormat(string? value, string? paramName)
    {
        throw new ArgumentException($"'{value}' is not a valid GUID format.", paramName);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowVectorDimensionMismatch(int actual, int expected, string? paramName)
    {
        throw new ArgumentException($"Vector dimension mismatch. Expected {expected}, got {actual}.", paramName);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentNull(string? paramName)
    {
        throw new ArgumentNullException(paramName);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowTopKOutOfRange(int topK, int maxValue)
    {
        throw new ArgumentOutOfRangeException(nameof(topK), topK, $"TopK must be between 1 and {maxValue}.");
    }

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowOperationCancelled()
    {
        throw new OperationCanceledException();
    }

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowCollectionNotFound(string collectionName)
    {
        throw new InvalidOperationException($"Collection '{collectionName}' not found.");
    }

    #endregion
}
