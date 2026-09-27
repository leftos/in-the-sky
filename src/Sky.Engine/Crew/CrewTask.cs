namespace Sky.Engine.Crew;

/// <summary>
/// One piece of crew work on the <see cref="TaskBoard"/>. It carries a claim priority, its place on the board, and a
/// hold priority, what it defends once running: a posted task pre-empts a running one only when its claim is above the
/// running task's hold.
/// </summary>
public sealed record CrewTask
{
    /// <summary>The task's id, unique among the tasks on one board.</summary>
    public required int Id { get; init; }

    /// <summary>What the task is for.</summary>
    public required TaskKind Kind { get; init; }

    /// <summary>The task's place on the board: a higher claim is taken first.</summary>
    public required int ClaimPriority { get; init; }

    /// <summary>What the task defends once running: only a claim strictly above it pre-empts it.</summary>
    public required int HoldPriority { get; init; }

    /// <summary>The zone the task belongs to; the board carries it for the caller's claim rules and never reads it.</summary>
    public required string Zone
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Zone));
            field = value;
        }
    }

    /// <summary>How many crew the task takes, at least 1; carried, not enforced (a cart is two postings met at a sync point).</summary>
    public required int CrewNeeded
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1, nameof(CrewNeeded));
            field = value;
        }
    }

    /// <summary>The tick the task was posted on; a pre-empted task returns to the board keeping it.</summary>
    public required long PostedTick { get; init; }
}
