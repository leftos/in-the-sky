namespace Sky.Engine.Events;

/// <summary>
/// The kinds of incident a passenger can raise: the five need failures of <c>docs/design/passengers.md</c> section 8, and
/// the fight an event escalates to (<c>docs/design/events.md</c>). Lua names each by its snake_case id.
/// </summary>
public enum IncidentKind
{
    /// <summary>A Bladder failure: the passenger could not reach a lavatory in time (<c>accident</c>).</summary>
    Accident,

    /// <summary>An Unease failure (<c>panic</c>).</summary>
    Panic,

    /// <summary>A Refreshment failure: the passenger walks to the galley to demand food (<c>food_demand</c>).</summary>
    FoodDemand,

    /// <summary>A Rest failure aimed at whoever keeps the passenger awake (<c>noise_complaint</c>).</summary>
    NoiseComplaint,

    /// <summary>A Boredom failure that disturbs the seats around the passenger (<c>disruptive_passenger</c>).</summary>
    DisruptivePassenger,

    /// <summary>A dispute between passengers that an event let escalate (<c>fight</c>).</summary>
    Fight,
}
