namespace Aspire_Full.Tests.Unit.Fixtures;

/// <summary>
/// A fake TimeProvider for testing that allows controlling the current time.
/// </summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FakeTimeProvider(DateTimeOffset initialTime)
    {
        _utcNow = initialTime;
    }

    public FakeTimeProvider() : this(DateTimeOffset.UtcNow)
    {
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    /// <summary>
    /// Advances the time by the specified duration.
    /// </summary>
    public void Advance(TimeSpan duration)
    {
        _utcNow = _utcNow.Add(duration);
    }

    /// <summary>
    /// Sets the current time to the specified value.
    /// </summary>
    public void SetUtcNow(DateTimeOffset value)
    {
        _utcNow = value;
    }
}
