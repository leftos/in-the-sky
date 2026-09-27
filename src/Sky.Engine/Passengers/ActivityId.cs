namespace Sky.Engine.Passengers;

/// <summary>
/// The interned id of one activity: its content name is interned to this integer when content loads (R9), so nothing at
/// simulation time carries the string.
/// </summary>
public readonly record struct ActivityId
{
    /// <summary>Creates the id from its interned integer.</summary>
    /// <param name="value">The interned value, at least 0.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is below 0.</exception>
    public ActivityId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Gets the interned integer, at least 0.</summary>
    public int Value { get; }
}
