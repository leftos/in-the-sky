namespace Sky.Engine.Time;

/// <summary>The fixed length of a sim tick and the tick counts of the sim's larger units.</summary>
public static class SimTime
{
    /// <summary>The sim milliseconds one tick covers.</summary>
    public const int TickMilliseconds = 250;

    /// <summary>The ticks in one sim minute.</summary>
    public const int TicksPerSimMinute = 60_000 / TickMilliseconds;

    /// <summary>The ticks in one sim hour.</summary>
    public const int TicksPerSimHour = 3_600_000 / TickMilliseconds;
}
