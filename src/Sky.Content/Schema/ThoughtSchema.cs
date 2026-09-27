namespace Sky.Content.Schema;

/// <summary><c>thoughts.json</c>: the thought catalogue of <c>passengers.md</c> section 10, with no words in M1.</summary>
/// <param name="Kinds">The thought kinds.</param>
public sealed record ThoughtsFile(IReadOnlyList<ThoughtKindSpec> Kinds);

/// <summary>Whether a thought is a complaint or praise.</summary>
public enum Valence
{
    /// <summary>A complaint.</summary>
    Grumble,

    /// <summary>Praise.</summary>
    Praise,
}

/// <summary>One kind of thought: how it is born, what lever would change it, and how long it lasts.</summary>
public sealed record ThoughtKindSpec
{
    /// <summary>Gets the kind's content id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets whether the kind is a grumble or praise.</summary>
    public required Valence Valence { get; init; }

    /// <summary>Gets the id of the engine hook that gives birth to it.</summary>
    public required string Hook { get; init; }

    /// <summary>Gets the lever or moment that would change it.</summary>
    public required string LeverTag { get; init; }

    /// <summary>Gets the lever tag when a neighbour is the subject, or null when it is always <see cref="LeverTag"/>.</summary>
    public string? NeighbourLeverTag { get; init; }

    /// <summary>Gets what the thought is about, or null when it has no subject.</summary>
    public string? Subject { get; init; }

    /// <summary>Gets the salience, 1 to 3; a new thought replaces the current one at equal or higher salience.</summary>
    public required int Salience { get; init; }

    /// <summary>Gets the sim minutes it lasts, or null for one that lasts until the passenger is first served; required either way.</summary>
    public required int? LastsMinutes { get; init; }

    /// <summary>Gets the number the hook compares against, in the hook's own unit, or null when it compares none.</summary>
    public double? Threshold { get; init; }
}
