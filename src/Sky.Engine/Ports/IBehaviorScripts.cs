using System.Diagnostics.CodeAnalysis;
using Sky.Engine.Passengers;
using Sky.Engine.Randomness;

namespace Sky.Engine.Ports;

/// <summary>
/// The port the flight scores behaviour and evaluates events through: one implementation over the Lua modules, one over
/// plain C# in tests. Scoring and event evaluation read facts and return values; nothing here changes the flight.
/// </summary>
/// <remarks>
/// An event runs as trigger, then describe and choices, then effects for the chosen branch, then release. A script
/// fault never escapes: the event is disabled for the flight and the call returns nothing.
/// </remarks>
public interface IBehaviorScripts
{
    /// <summary>Gets the id of every event module loaded for the flight, disabled ones included, in load order.</summary>
    IReadOnlyList<string> EventIds { get; }

    /// <summary>Scores every candidate activity for one passenger, in one call.</summary>
    /// <param name="facts">The passenger's facts.</param>
    /// <param name="candidates">The activities to score, in the order their scores are written.</param>
    /// <param name="scores">
    /// Receives one score per candidate at the same index. A score is at least 0, and 0 means the activity is
    /// unavailable.
    /// </param>
    void ScoreActivities(in PassengerFacts facts, ReadOnlySpan<ActivityId> candidates, Span<double> scores);

    /// <summary>
    /// Reports whether an event is disabled for the flight, so that the flight can skip it and journal why it was disabled.
    /// </summary>
    /// <param name="eventId">One of <see cref="EventIds"/>.</param>
    /// <param name="reason">Why the event was disabled, or <see langword="null"/> when it is enabled.</param>
    /// <returns><see langword="true"/> when the event is disabled.</returns>
    bool IsEventDisabled(string eventId, [NotNullWhen(true)] out string? reason);

    /// <summary>Asks one event's trigger whether it fires now.</summary>
    /// <param name="eventId">One of <see cref="EventIds"/>.</param>
    /// <param name="ctx">What the trigger reads.</param>
    /// <param name="random">The stream the trigger rolls its chance from, and the only one it may draw from.</param>
    /// <returns>
    /// What the trigger recorded, or <see langword="null"/> when it did not fire, the stage is outside the event's
    /// phases, or the event is disabled.
    /// </returns>
    EventFacts? Trigger(string eventId, in EventContext ctx, SimRandom random);

    /// <summary>Gets the scene a crew member would notice, in one or two sentences.</summary>
    /// <param name="facts">Facts a trigger returned and that are not yet released.</param>
    /// <returns>The scene, or an empty string when the event is disabled.</returns>
    string Describe(EventFacts facts);

    /// <summary>Gets the two to four choices the event offers for these facts.</summary>
    /// <param name="facts">Facts a trigger returned and that are not yet released.</param>
    /// <returns>
    /// The choices, one of them the crew-free <c>leave</c>; or an empty list when the event is disabled.
    /// </returns>
    IReadOnlyList<EventChoice> Choices(EventFacts facts);

    /// <summary>Gets the delayed consequences of one chosen branch.</summary>
    /// <param name="facts">Facts a trigger returned and that are not yet released.</param>
    /// <param name="choiceId">
    /// The id of one of the choices <see cref="Choices"/> offered for these facts; the choice ids always include
    /// <c>leave</c>, the crew-free choice a task nobody starts resolves with.
    /// </param>
    /// <param name="random">The only stream the effects may draw from.</param>
    /// <returns>The consequences in the order the module listed them, or an empty list when the event is disabled.</returns>
    /// <exception cref="InvalidOperationException">
    /// The event is enabled and <paramref name="choiceId"/> is not among the choices <see cref="Choices"/> last offered
    /// for these facts, or the facts were released: a caller bug.
    /// </exception>
    IReadOnlyList<DelayedConsequence> Effects(EventFacts facts, string choiceId, SimRandom random);

    /// <summary>
    /// Drops the recorded facts once the event has resolved; the facts may not be passed back after this. Facts of an
    /// event disabled after it fired are still released.
    /// </summary>
    /// <param name="facts">Facts a trigger returned and that are not yet released.</param>
    void Release(EventFacts facts);
}
