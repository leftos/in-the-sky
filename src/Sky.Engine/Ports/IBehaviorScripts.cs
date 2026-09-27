using Sky.Engine.Passengers;

namespace Sky.Engine.Ports;

/// <summary>
/// The port the flight scores behaviour through: one implementation over the Lua activity modules, one over plain C# in
/// tests. Scoring is a pure read of the facts; nothing here changes the flight.
/// </summary>
public interface IBehaviorScripts
{
    /// <summary>Scores every candidate activity for one passenger, in one call.</summary>
    /// <param name="facts">The passenger's facts.</param>
    /// <param name="candidates">The activities to score, in the order their scores are written.</param>
    /// <param name="scores">
    /// Receives one score per candidate at the same index. A score is at least 0, and 0 means the activity is
    /// unavailable.
    /// </param>
    void ScoreActivities(in PassengerFacts facts, ReadOnlySpan<ActivityId> candidates, Span<double> scores);
}
