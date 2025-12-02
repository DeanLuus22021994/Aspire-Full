using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ArcFaceSandbox.VectorStore;

internal static class ThrowHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull<T>([NotNull] T? instance, [CallerArgumentExpression(nameof(instance))] string? paramName = null)
    {
        if (instance is null)
        {
            ThrowArgumentNull(paramName);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guid ValidateGuid(string? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ThrowInvalidGuid(value, paramName);
        }

        if (!Guid.TryParse(value, out var guid))
        {
            ThrowInvalidGuid(value, paramName);
        }

        return guid;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateVectorLength(int actual, int expected, [CallerArgumentExpression(nameof(actual))] string? paramName = null)
    {
        if (actual != expected)
        {
            ThrowVectorLength(actual, expected, paramName);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateTopK(int topK, int max = 10_000)
    {
        if (topK <= 0 || topK > max)
        {
            ThrowTopK(topK, max);
        }
    }

    #region Throws

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentNull(string? paramName) => throw new ArgumentNullException(paramName);

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidGuid(string? value, string? paramName) => throw new ArgumentException($"'{value}' is not a valid GUID.", paramName);

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowVectorLength(int actual, int expected, string? paramName) => throw new ArgumentException($"Vector length mismatch. Expected {expected}, got {actual}.", paramName);

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowTopK(int topK, int max) => throw new ArgumentOutOfRangeException(nameof(topK), topK, $"Value must be between 1 and {max}.");

    #endregion
}
