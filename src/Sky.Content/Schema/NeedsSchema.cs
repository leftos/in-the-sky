using Sky.Engine.Needs;

namespace Sky.Content.Schema;

/// <summary>
/// <c>needs.json</c>: the need rates, cascades and distress the Engine reads, and the numbers <c>passengers.md</c> sections 2,
/// 7 and 8 give a passenger's needs (starting draws, gate conditions, Unease pushes, seat comfort, contagion, incidents).
/// </summary>
public sealed record NeedsFile
{
    /// <summary>Gets the base need rates per sim hour and the Unease half-life.</summary>
    public required NeedRateSettings Rates { get; init; }

    /// <summary>Gets the cascade rules, in the order their modifiers are written.</summary>
    public required IReadOnlyList<CascadeRule> CascadeRules { get; init; }

    /// <summary>Gets the distress terms, exactly one per need.</summary>
    public required IReadOnlyList<DistressTerm> DistressTerms { get; init; }

    /// <summary>Gets where the distress bands read by sight begin.</summary>
    public required DistressBandsSpec DistressBands { get; init; }

    /// <summary>Gets the starting need draws at boarding.</summary>
    public required StartingNeedsSpec Starting { get; init; }

    /// <summary>Gets what the gate delay and the concessions add at boarding.</summary>
    public required GateConditionsSpec GateConditions { get; init; }

    /// <summary>Gets the Unease push sources and one-off pulses.</summary>
    public required UneasePushSpec UneasePushes { get; init; }

    /// <summary>Gets the seat comfort modifiers of the reference layout.</summary>
    public required SeatComfortSpec SeatComfort { get; init; }

    /// <summary>Gets Unease contagion, the calming source and witnessing.</summary>
    public required ContagionSpec Contagion { get; init; }

    /// <summary>Gets the incident kinds a need failure raises, one sustain gate each.</summary>
    public required IReadOnlyList<IncidentKindSpec> Incidents { get; init; }

    /// <summary>Gets how far below its threshold a need must fall before the same incident kind can be raised again.</summary>
    public required double IncidentResetMargin { get; init; }

    /// <summary>Gets how many holders a nav graph node of each kind admits.</summary>
    public required NodeCapacitiesSpec NodeCapacities { get; init; }
}

/// <summary>A uniform draw between two values, both included.</summary>
/// <param name="Min">The lowest value.</param>
/// <param name="Max">The highest value.</param>
public sealed record DrawRange(double Min, double Max);

/// <summary>Where the distress bands read by sight begin; below <paramref name="UneasyFrom"/> a passenger reads calm.</summary>
/// <param name="UneasyFrom">The distress at which a passenger reads uneasy.</param>
/// <param name="DistressedFrom">The distress at which a passenger reads distressed.</param>
public sealed record DistressBandsSpec(double UneasyFrom, double DistressedFrom);

/// <summary>The needs drawn at boarding; Rest comes from the body clock and Unease starts at the passenger's baseline.</summary>
/// <param name="Refreshment">The Refreshment draw.</param>
/// <param name="Bladder">The Bladder draw.</param>
/// <param name="Boredom">The Boredom draw.</param>
/// <param name="GroupSpread">How far apart a group's Refreshment and Boredom draws may be.</param>
public sealed record StartingNeedsSpec(DrawRange Refreshment, DrawRange Bladder, DrawRange Boredom, double GroupSpread);

/// <summary>What the scenario's gate delay D (minutes) and its concessions add to each passenger's needs at boarding.</summary>
public sealed record GateConditionsSpec
{
    /// <summary>Gets the Refreshment added when the outlets were closed.</summary>
    public required double ClosedRefreshment { get; init; }

    /// <summary>Gets the Refreshment added per minute of delay with the outlets open.</summary>
    public required double RefreshmentPerMinuteOpen { get; init; }

    /// <summary>Gets the Refreshment added per minute of delay with the outlets closed.</summary>
    public required double RefreshmentPerMinuteClosed { get; init; }

    /// <summary>Gets the most Refreshment the delay itself adds.</summary>
    public required double RefreshmentCap { get; init; }

    /// <summary>Gets the Boredom added per minute of delay.</summary>
    public required double BoredomPerMinute { get; init; }

    /// <summary>Gets the most Boredom the delay adds.</summary>
    public required double BoredomCap { get; init; }

    /// <summary>Gets the minutes of delay that add no Unease.</summary>
    public required double UneaseGraceMinutes { get; init; }

    /// <summary>Gets the Unease added per minute of delay past the grace.</summary>
    public required double UneasePerMinute { get; init; }

    /// <summary>Gets the most Unease the delay adds above the baseline.</summary>
    public required double UneaseCap { get; init; }

    /// <summary>Gets the delay in minutes from which every passenger boards late and fed up.</summary>
    public required double LateAndFedUpFromMinutes { get; init; }

    /// <summary>Gets the Context-class Unease factor a late-and-fed-up passenger carries until first served.</summary>
    public required double LateAndFedUpUneaseFactor { get; init; }

    /// <summary>Gets the highest starting Refreshment, so nobody boards inside a failure window.</summary>
    public required double StartingRefreshmentCap { get; init; }
}

/// <summary>The sources that push Unease up, per sim hour unless named a pulse.</summary>
public sealed record UneasePushSpec
{
    /// <summary>Gets the push of being aboard.</summary>
    public required double AboardPerHour { get; init; }

    /// <summary>Gets the push during takeoff and landing.</summary>
    public required double TakeoffAndLandingPerHour { get; init; }

    /// <summary>Gets the push during climb and descent.</summary>
    public required double ClimbAndDescentPerHour { get; init; }

    /// <summary>Gets the push in light turbulence.</summary>
    public required double LightTurbulencePerHour { get; init; }

    /// <summary>Gets the push in moderate turbulence.</summary>
    public required double ModerateTurbulencePerHour { get; init; }

    /// <summary>Gets the push of a delay past the planned pushback.</summary>
    public required double LatePushbackPerHour { get; init; }

    /// <summary>Gets the minutes past the planned pushback before the late-pushback push starts.</summary>
    public required double LatePushbackGraceMinutes { get; init; }

    /// <summary>Gets the push of the passenger's own call button left unanswered.</summary>
    public required double UnansweredCallPerHour { get; init; }

    /// <summary>Gets the minutes a call waits unanswered before its push starts.</summary>
    public required double UnansweredCallGraceMinutes { get; init; }

    /// <summary>Gets the one-off pulse of being woken from sleep.</summary>
    public required double WokenPulse { get; init; }

    /// <summary>Gets the sim minutes a one-off pulse is spread over.</summary>
    public required double PulseMinutes { get; init; }
}

/// <summary>A seat's Context-class factors on Rest and Unease.</summary>
/// <param name="Rest">The factor on Rest.</param>
/// <param name="Unease">The factor on Unease.</param>
public sealed record ComfortModifiers(double Rest, double Unease);

/// <summary>The seat comfort modifiers of the reference layout, by seat.</summary>
/// <param name="Business">Any business seat.</param>
/// <param name="EconomyMiddle">An economy middle seat.</param>
/// <param name="EconomyWindowOrAisle">An economy window or aisle seat.</param>
public sealed record SeatComfortSpec(ComfortModifiers Business, ComfortModifiers EconomyMiddle, ComfortModifiers EconomyWindowOrAisle);

/// <summary>Unease contagion between neighbours, the off-duty crew calming source, and witnessing an incident.</summary>
public sealed record ContagionSpec
{
    /// <summary>Gets the Unease above which a neighbour spreads contagion.</summary>
    public required double Threshold { get; init; }

    /// <summary>Gets the contagion weight of a neighbour beside the passenger.</summary>
    public required double BesideWeight { get; init; }

    /// <summary>Gets the contagion weight of the neighbour across the aisle.</summary>
    public required double AcrossAisleWeight { get; init; }

    /// <summary>Gets the Unease an off-duty crew passenger must stay under to calm their neighbours.</summary>
    public required double CalmingThreshold { get; init; }

    /// <summary>Gets the calming weight beside the off-duty crew passenger.</summary>
    public required double CalmingBesideWeight { get; init; }

    /// <summary>Gets the calming weight across the aisle.</summary>
    public required double CalmingAcrossAisleWeight { get; init; }

    /// <summary>Gets how many rows ahead and behind an incident is witnessed.</summary>
    public required int WitnessRows { get; init; }

    /// <summary>Gets the Unease pulse of witnessing an incident raised.</summary>
    public required double WitnessRaisedPulse { get; init; }

    /// <summary>Gets the Unease pulse of witnessing a missed incident's consequence.</summary>
    public required double WitnessMissedPulse { get; init; }
}

/// <summary>One incident kind: the need that raises it, its sustain gate and its escalation deadline.</summary>
public sealed record IncidentKindSpec
{
    /// <summary>Gets the incident kind's content id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the need whose failure raises it.</summary>
    public required Need Need { get; init; }

    /// <summary>Gets the value at or above which the need counts toward the failure, in [0, 100).</summary>
    public required double Threshold { get; init; }

    /// <summary>Gets the sim minutes the need must hold at or above the threshold.</summary>
    public required int WindowMinutes { get; init; }

    /// <summary>Gets the sim minutes crew have to reach the passenger before the incident is missed.</summary>
    public required int DeadlineMinutes { get; init; }

    /// <summary>Gets whether only ticks the passenger is awake count, as for a noise complaint.</summary>
    public bool AwakeOnly { get; init; }
}

/// <summary>How many holders a nav graph node of each kind admits, each at least one.</summary>
public sealed record NodeCapacitiesSpec
{
    /// <summary>Gets the holders an aisle slot admits before a squeeze.</summary>
    public required int AisleSlot { get; init; }

    /// <summary>Gets the holders a seat admits.</summary>
    public required int Seat { get; init; }

    /// <summary>Gets the holders a door admits.</summary>
    public required int Door { get; init; }

    /// <summary>Gets the holders a lav admits.</summary>
    public required int Lav { get; init; }

    /// <summary>Gets the holders a lav queue admits.</summary>
    public required int LavQueue { get; init; }

    /// <summary>Gets the holders a galley admits.</summary>
    public required int Galley { get; init; }
}
