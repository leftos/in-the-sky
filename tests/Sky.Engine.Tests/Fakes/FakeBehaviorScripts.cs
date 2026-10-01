using System.Diagnostics.CodeAnalysis;
using Sky.Engine.Passengers;
using Sky.Engine.Ports;
using Sky.Engine.Randomness;

namespace Sky.Engine.Tests.Fakes;

/// <summary>
/// The behaviour port in plain C#, with no Lua: each activity scores what the test set for it (0 when unset) or, when
/// <see cref="Scorer"/> is set, what that function gives it; every scoring
/// call's facts are recorded, and no event is loaded.
/// </summary>
internal sealed class FakeBehaviorScripts : IBehaviorScripts
{
    private readonly Dictionary<ActivityId, double> activityScores = [];

    private readonly List<PassengerFacts> scoredFacts = [];

    /// <inheritdoc/>
    public IReadOnlyList<string> EventIds { get; } = [];

    /// <summary>Gets the facts of every <see cref="ScoreActivities"/> call, in call order; each holds the live need set it was given.</summary>
    public IReadOnlyList<PassengerFacts> ScoredFacts => scoredFacts;

    /// <summary>Gets or sets a function scoring each candidate from the facts; when set, the scores <see cref="SetScore"/> set are ignored.</summary>
    public Func<PassengerFacts, ActivityId, double>? Scorer { get; set; }

    /// <summary>Sets the score an activity gets from every later call.</summary>
    /// <param name="activity">The activity.</param>
    /// <param name="score">Its score, at least 0.</param>
    public void SetScore(ActivityId activity, double score) => activityScores[activity] = score;

    /// <inheritdoc/>
    public void ScoreActivities(in PassengerFacts facts, ReadOnlySpan<ActivityId> candidates, Span<double> scores)
    {
        scoredFacts.Add(facts);
        for (int index = 0; index < candidates.Length; index++)
        {
            scores[index] = Scorer is null ? activityScores.GetValueOrDefault(candidates[index]) : Scorer(facts, candidates[index]);
        }
    }

    /// <inheritdoc/>
    public bool IsEventDisabled(string eventId, [NotNullWhen(true)] out string? reason)
    {
        reason = null;
        return false;
    }

    /// <inheritdoc/>
    public EventFacts? Trigger(string eventId, in EventContext ctx, SimRandom random) => null;

    /// <inheritdoc/>
    public string Describe(EventFacts facts) => string.Empty;

    /// <inheritdoc/>
    public IReadOnlyList<EventChoice> Choices(EventFacts facts) => [];

    /// <inheritdoc/>
    public IReadOnlyList<DelayedConsequence> Effects(EventFacts facts, string choiceId, SimRandom random) => [];

    /// <inheritdoc/>
    public void Release(EventFacts facts) { }
}
