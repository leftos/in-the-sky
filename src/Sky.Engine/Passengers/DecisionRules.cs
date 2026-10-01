using System.Collections.Immutable;

namespace Sky.Engine.Passengers;

/// <summary>
/// The numbers of a passenger's activity decision; content supplies them, and the flight refuses a bad one at construction,
/// naming the field.
/// </summary>
public sealed record DecisionRules
{
    /// <summary>Gets the ticks between two decisions of one passenger, at least 1; passengers are staggered by id across them.</summary>
    public required int CadenceTicks { get; init; }

    /// <summary>Gets the utility points added to the current activity's score when it is a candidate scoring above 0, at least 0.</summary>
    public required double KeepCurrentBias { get; init; }

    /// <summary>Gets the activities a passenger chooses among, in the order they are scored; never empty.</summary>
    public required ImmutableArray<ActivityId> Candidates { get; init; }

    /// <summary>Gets the activity every passenger is on before their first decision; one of <see cref="Candidates"/>.</summary>
    public required ActivityId Initial { get; init; }
}
