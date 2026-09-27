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

    /// <summary>
    /// Gets the number the hook compares against, or null when it compares none. The unit is the hook's own, so one field
    /// holds unlike quantities by <see cref="Hook"/>, taken from <c>passengers.md</c> section 10's "Born when" clauses:
    /// <c>decision_point</c> (<c>still_waiting_for_drinks</c>, <c>too_bright_to_sleep</c>, <c>scared_of_bumps</c> and
    /// <c>bored_no_screen</c>) and <c>served</c> (<c>served_late</c>, <c>drink_welcome</c>) hold a need value, 0 to 100;
    /// <c>lav_queue_joined</c> the people already ahead in the queue; <c>aisle_wait</c> and <c>call_light_on</c> the sim
    /// minutes waited so far; <c>call_answered</c> the sim minutes the call waited; and <c>woke_naturally</c> the sim
    /// minutes asleep. The other hooks of <see cref="Sky.Content.Validation.ThoughtCatalogueValidator.Hooks"/> —
    /// <c>boarded</c>, <c>lav_visit_finished</c>, <c>witness_pulse</c>, <c>woken</c> and <c>unease_lowered_by_crew</c> —
    /// compare none, so a kind born from one of them leaves this null.
    /// </summary>
    public double? Threshold { get; init; }
}
