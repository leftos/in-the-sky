namespace Sky.Engine.Cabin;

/// <summary>How many holders a node of each kind admits before it is full; every capacity is at least one.</summary>
public sealed record NodeCapacities
{
    /// <summary>Gets the holders an aisle slot admits before a squeeze.</summary>
    public required int AisleSlot
    {
        get;
        init => field = AtLeastOne(value, nameof(AisleSlot));
    }

    /// <summary>Gets the holders a seat admits.</summary>
    public required int Seat
    {
        get;
        init => field = AtLeastOne(value, nameof(Seat));
    }

    /// <summary>Gets the holders a door admits.</summary>
    public required int Door
    {
        get;
        init => field = AtLeastOne(value, nameof(Door));
    }

    /// <summary>Gets the holders a lavatory admits.</summary>
    public required int Lav
    {
        get;
        init => field = AtLeastOne(value, nameof(Lav));
    }

    /// <summary>Gets the holders a lavatory queue admits.</summary>
    public required int LavQueue
    {
        get;
        init => field = AtLeastOne(value, nameof(LavQueue));
    }

    /// <summary>Gets the holders a galley admits.</summary>
    public required int Galley
    {
        get;
        init => field = AtLeastOne(value, nameof(Galley));
    }

    // No default arm, so a new NodeKind member fails the build here (CS8509). CS8524 only reports casts of unnamed values,
    // which no graph carries.
#pragma warning disable CS8524
    /// <summary>Returns the capacity of a node kind.</summary>
    /// <param name="kind">The node kind.</param>
    /// <returns>The holders a node of that kind admits.</returns>
    public int For(NodeKind kind) =>
        kind switch
        {
            NodeKind.AisleSlot => AisleSlot,
            NodeKind.Seat => Seat,
            NodeKind.Door => Door,
            NodeKind.Lav => Lav,
            NodeKind.LavQueue => LavQueue,
            NodeKind.Galley => Galley,
        };
#pragma warning restore CS8524

    private static int AtLeastOne(int value, string paramName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1, paramName);
        return value;
    }
}
