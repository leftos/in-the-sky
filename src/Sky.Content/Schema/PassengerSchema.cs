using System.Text.Json.Serialization;
using Sky.Engine.Needs;

namespace Sky.Content.Schema;

/// <summary>
/// <c>traits.json</c>: the passenger traits and belongings of <c>passengers.md</c> section 4, interned to trait ids in
/// declaration order, and the pairs that never co-occur.
/// </summary>
/// <param name="DefaultUneaseBaseline">The Unease baseline of a passenger with no Unease trait.</param>
/// <param name="Traits">The traits and belongings; the one at index i is <c>new TraitId(i)</c>.</param>
/// <param name="ForbiddenPairs">The trait pairs a passenger never draws together.</param>
public sealed record TraitsFile(double DefaultUneaseBaseline, IReadOnlyList<TraitSpec> Traits, IReadOnlyList<TraitPair> ForbiddenPairs);

/// <summary>One passenger trait; a trait with a <see cref="Share"/> is a belonging, drawn on its own roll.</summary>
public sealed record TraitSpec
{
    /// <summary>Gets the trait's content id, the name a Lua module asks <c>has_trait</c> for.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the Trait-class factors on need rates.</summary>
    [JsonInclude]
    public IReadOnlyList<TraitModifier> Modifiers { get; internal set; } = [];

    /// <summary>Gets the Unease baseline the trait sets, or null when it sets none.</summary>
    public double? UneaseBaseline { get; init; }

    /// <summary>Gets the factor on every wake-up chance, 1 for none.</summary>
    [JsonInclude]
    public double WakeChanceFactor { get; internal set; } = 1.0;

    /// <summary>Gets the weight of the trait in an adult's draw, or null when adults never draw it.</summary>
    public double? AdultWeight { get; init; }

    /// <summary>Gets the weight on a <c>business</c> trip, or null when it is the adult weight.</summary>
    public double? BusinessTripWeight { get; init; }

    /// <summary>Gets whether a child may draw the trait as their one optional trait.</summary>
    public bool ChildOptional { get; init; }

    /// <summary>Gets whether every child carries the trait, undrawn.</summary>
    public bool GivenToEveryChild { get; init; }

    /// <summary>Gets the shares a belonging is carried at, or null for a trait that is not a belonging.</summary>
    public BelongingShare? Share { get; init; }
}

/// <summary>One Trait-class factor on a need's rate.</summary>
/// <param name="Need">The need whose rate the factor scales.</param>
/// <param name="Factor">The factor.</param>
public sealed record TraitModifier(Need Need, double Factor);

/// <summary>The chance each passenger carries a belonging, rolled independently.</summary>
/// <param name="Adults">The share of adults.</param>
/// <param name="BusinessTrips">The share of adults on a <c>business</c> trip.</param>
/// <param name="Children">The share of children.</param>
public sealed record BelongingShare(double Adults, double BusinessTrips, double Children);

/// <summary>Two traits a passenger never carries together.</summary>
/// <param name="First">One trait's id.</param>
/// <param name="Second">The other trait's id.</param>
public sealed record TraitPair(string First, string Second);

/// <summary>
/// <c>activities.json</c>: the scored activities of <c>passengers.md</c> section 3, interned to activity ids in declaration
/// order, each with a Lua module at <c>activities/&lt;id&gt;.lua</c>; what answering a call does; and the seat and lav numbers.
/// </summary>
/// <param name="Activities">The activities; the one at index i is <c>new ActivityId(i)</c>.</param>
/// <param name="CallReasons">The reasons a passenger presses the call button, and what answering each does.</param>
/// <param name="Movement">Getting out of a seat, choosing a lav and giving up on an overflowing queue.</param>
public sealed record ActivitiesFile(IReadOnlyList<ActivitySpec> Activities, IReadOnlyList<CallReasonSpec> CallReasons, MovementSpec Movement);

/// <summary>Where on the nav graph an activity happens.</summary>
public enum ActivitySite
{
    /// <summary>In the passenger's seat.</summary>
    Seat,

    /// <summary>At a lav, by way of its queue.</summary>
    Lav,

    /// <summary>At the galley.</summary>
    Galley,
}

/// <summary>When an activity's change to a need lands.</summary>
public enum EffectTiming
{
    /// <summary>At a rate per sim hour for as long as the activity runs.</summary>
    PerHour,

    /// <summary>Spread evenly over <see cref="ActivityEffect.OverMinutes"/>, or the activity's own duration when that is null.</summary>
    Spread,

    /// <summary>Whole, when the activity starts.</summary>
    OnStart,

    /// <summary>Whole, when the activity completes.</summary>
    OnCompletion,

    /// <summary>The need is set to the change's value when the activity completes.</summary>
    SetOnCompletion,
}

/// <summary>The cabin lighting an activity effect applies in.</summary>
public enum CabinLighting
{
    /// <summary>Either lighting.</summary>
    Any,

    /// <summary>Only with the cabin dimmed.</summary>
    Dimmed,

    /// <summary>Only with the cabin lights up.</summary>
    Up,
}

/// <summary>One scored activity.</summary>
public sealed record ActivitySpec
{
    /// <summary>Gets the activity's content id, which also names its module file.</summary>
    public required string Id { get; init; }

    /// <summary>Gets where on the nav graph the activity happens.</summary>
    public required ActivitySite Site { get; init; }

    /// <summary>Gets whether the activity needs crew: it posts a task, or crew deliver it.</summary>
    public required bool NeedsCrew { get; init; }

    /// <summary>Gets the duration in sim minutes, or null for an activity that runs until the next decision point.</summary>
    public DrawRange? Duration { get; init; }

    /// <summary>Gets a child's duration in sim minutes, or null when it is <see cref="Duration"/>.</summary>
    public DrawRange? ChildDuration { get; init; }

    /// <summary>Gets what the activity does to needs.</summary>
    [JsonInclude]
    public IReadOnlyList<ActivityEffect> Effects { get; internal set; } = [];

    /// <summary>Gets the needs whose base rise pauses while the activity runs.</summary>
    [JsonInclude]
    public IReadOnlyList<Need> Pauses { get; internal set; } = [];

    /// <summary>Gets the modifier the activity leaves behind when it completes, or null for none.</summary>
    public AfterModifier? After { get; init; }
}

/// <summary>One change an activity makes to a need.</summary>
/// <param name="Need">The need changed.</param>
/// <param name="Change">The change, negative for relief; for <see cref="EffectTiming.SetOnCompletion"/> the value set.</param>
/// <param name="Timing">When the change lands.</param>
/// <param name="OverMinutes">The sim minutes a <see cref="EffectTiming.Spread"/> change lands over, or null for the activity's duration.</param>
/// <param name="Lighting">The cabin lighting the change applies in.</param>
public sealed record ActivityEffect(
    Need Need,
    double Change,
    EffectTiming Timing,
    double? OverMinutes = null,
    CabinLighting Lighting = CabinLighting.Any
);

/// <summary>A modifier an activity leaves on a need's rate for a while after it completes, such as the post-meal slump.</summary>
/// <param name="Need">The need whose rate is scaled.</param>
/// <param name="Class">The class of source the factor belongs to.</param>
/// <param name="Factor">The factor.</param>
/// <param name="Minutes">The sim minutes it lasts.</param>
public sealed record AfterModifier(Need Need, SourceClass Class, double Factor, double Minutes);

/// <summary>A reason a passenger presses the call button, and what answering it does.</summary>
/// <param name="Id">The reason's content id.</param>
/// <param name="Granted">The need changes when the request is granted.</param>
/// <param name="Refused">The need changes when it is refused.</param>
public sealed record CallReasonSpec(string Id, IReadOnlyList<NeedChange> Granted, IReadOnlyList<NeedChange> Refused);

/// <summary>A one-off change to a need, whole or spread over a time.</summary>
/// <param name="Need">The need changed.</param>
/// <param name="Change">The change, negative for relief.</param>
/// <param name="OverMinutes">The sim minutes it is spread over, or null for a whole change.</param>
public sealed record NeedChange(Need Need, double Change, double? OverMinutes = null);

/// <summary>The numbers of getting out of a seat and choosing a lav.</summary>
public sealed record MovementSpec
{
    /// <summary>Gets the ticks each occupied seat crossed adds.</summary>
    public required int OccupiedSeatCrossTicks { get; init; }

    /// <summary>Gets the ticks a crossed seat with its tray down adds.</summary>
    public required int TrayDownCrossTicks { get; init; }

    /// <summary>Gets the Unease a passenger eating at a crossed tray takes.</summary>
    public required double TrayDownEaterUnease { get; init; }

    /// <summary>Gets the chance a crossed sleeper wakes.</summary>
    public required double CrossedSleeperWakeChance { get; init; }

    /// <summary>Gets the minutes each person already queued adds to a lav's pick cost.</summary>
    public required double LavQueueMinutesPerPerson { get; init; }

    /// <summary>Gets the minutes a passenger overflowing a lav queue waits before going back to their seat.</summary>
    public required double OverflowGiveUpMinutes { get; init; }

    /// <summary>Gets the Bladder under which an overflowing passenger gives up.</summary>
    public required double OverflowGiveUpBelowBladder { get; init; }
}
