namespace Sky.Engine.Time;

/// <summary>Turns elapsed sim milliseconds into whole ticks, carrying the part of a tick not yet due to the next call.</summary>
public sealed class TickAccumulator
{
    /// <summary>Gets the sim milliseconds carried toward the next tick, always less than one tick.</summary>
    public long RemainderMilliseconds { get; private set; }

    /// <summary>Adds elapsed sim milliseconds and returns the whole ticks now due.</summary>
    /// <param name="elapsedMilliseconds">The sim milliseconds elapsed since the previous call; never negative.</param>
    /// <returns>The whole ticks due, the remainder carried to the next call.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The elapsed milliseconds are negative.</exception>
    public int Add(long elapsedMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedMilliseconds);
        long total = checked(RemainderMilliseconds + elapsedMilliseconds);
        RemainderMilliseconds = total % SimTime.TickMilliseconds;
        return checked((int)(total / SimTime.TickMilliseconds));
    }
}
