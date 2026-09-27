using System.Text.Json.Serialization;

namespace Sky.Content.Schema;

/// <summary>
/// <c>crew.json</c>: the crew traits and the roster of <c>crew.md</c> "The crew model", fatigue, and the fixed timings of
/// a service round.
/// </summary>
/// <param name="Traits">The crew traits a roster entry may name.</param>
/// <param name="Roster">The crew members.</param>
/// <param name="Fatigue">How fatigue rises and falls.</param>
/// <param name="Service">The fixed timings of carts and hand service.</param>
public sealed record CrewFile(
    IReadOnlyList<CrewTraitSpec> Traits,
    IReadOnlyList<CrewMemberSpec> Roster,
    FatigueSpec Fatigue,
    ServiceTimingSpec Service
);

/// <summary>One crew member.</summary>
/// <param name="Id">The crew member's content id.</param>
/// <param name="Competence">Experience and skill, 0 to 1.</param>
/// <param name="Empathy">How closely they attend to the person in front of them, 0 to 1.</param>
/// <param name="StartingFatigue">Fatigue at the start of the flight, 0 to 100.</param>
/// <param name="Trait">The id of their one crew trait, or null for none; the field is required either way.</param>
public sealed record CrewMemberSpec(string Id, double Competence, double Empathy, double StartingFatigue, string? Trait);

/// <summary>One crew trait: factors on the crew member's focus, strain, service and relief, each 1 when the trait leaves it alone.</summary>
public sealed record CrewTraitSpec
{
    /// <summary>Gets the trait's content id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the factor on the fatigue term in focus.</summary>
    [JsonInclude]
    public double FocusFatigueWeightFactor { get; internal set; } = 1.0;

    /// <summary>Gets the factor on focus while at or over the redline.</summary>
    [JsonInclude]
    public double FocusFactorOverRedline { get; internal set; } = 1.0;

    /// <summary>Gets the factor on pre-emption strain steps.</summary>
    [JsonInclude]
    public double PreemptionStrainFactor { get; internal set; } = 1.0;

    /// <summary>Gets the factor on the on-task strain rate.</summary>
    [JsonInclude]
    public double OnTaskStrainFactor { get; internal set; } = 1.0;

    /// <summary>Gets the factor on service time per row.</summary>
    [JsonInclude]
    public double ServiceTimeFactor { get; internal set; } = 1.0;

    /// <summary>Gets the factor on the Unease relief of a check-in stop or an answered call.</summary>
    [JsonInclude]
    public double UneaseReliefFactor { get; internal set; } = 1.0;

    /// <summary>Gets the factor on the dwell of a check-in stop or an answered call.</summary>
    [JsonInclude]
    public double DwellFactor { get; internal set; } = 1.0;
}

/// <summary>How a crew member's fatigue moves.</summary>
/// <param name="RisePerHour">The rise per sim hour on duty.</param>
/// <param name="OverRedlineFactor">The factor on the rise while strain is over the redline.</param>
/// <param name="BreakFallPerMinute">The fall per sim minute on a galley break.</param>
public sealed record FatigueSpec(double RisePerHour, double OverRedlineFactor, double BreakFallPerMinute);

/// <summary>The fixed timings of a service round, carts and hand service alike.</summary>
public sealed record ServiceTimingSpec
{
    /// <summary>Gets a cart's speed as a share of walking pace.</summary>
    public required double CartSpeedFactor { get; init; }

    /// <summary>Gets the seconds a drinks cart spends per row, both crew working.</summary>
    public required double DrinksSecondsPerRow { get; init; }

    /// <summary>Gets the seconds a meal cart spends per row, both crew working.</summary>
    public required double MealSecondsPerRow { get; init; }

    /// <summary>Gets the minutes the seatbelt sign stays on mid-round before the carts return to the galley.</summary>
    public required double SignOnReturnMinutes { get; init; }

    /// <summary>Gets the minutes before a round's start that the business hand service is posted.</summary>
    public required double HandServiceLeadMinutes { get; init; }

    /// <summary>Gets the seconds per business row of hand-served drinks.</summary>
    public required double HandDrinksSecondsPerRow { get; init; }

    /// <summary>Gets the seconds per business row of a hand-served meal.</summary>
    public required double HandMealSecondsPerRow { get; init; }
}
