using Sky.Engine.Flight;

namespace Sky.Content.Schema;

/// <summary>
/// <c>scenarios/&lt;id&gt;.json</c>: one flight's setup. The four M1 levers (OD4: the service plan, locked lavs, the lighting
/// plan, and crew count with zone assignment), the gate conditions, the system switches and the phase timeline.
/// </summary>
public sealed record ScenarioFile
{
    /// <summary>Gets the scenario's id, which is also its file name.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the id of the layout the flight uses.</summary>
    public required string Layout { get; init; }

    /// <summary>Gets how long passengers waited past the scheduled boarding time, in minutes.</summary>
    public required int GateDelayMinutes { get; init; }

    /// <summary>Gets whether the airside food and drink outlets were open during the wait.</summary>
    public required bool ConcessionsOpen { get; init; }

    /// <summary>Gets which systems run.</summary>
    public required SystemSwitches Systems { get; init; }

    /// <summary>Gets the service rounds, in the order they run.</summary>
    public required IReadOnlyList<ServiceRound> ServicePlan { get; init; }

    /// <summary>Gets the fixture ids of the lavs locked for the flight; every other lav is open.</summary>
    public required IReadOnlyList<string> LockedLavs { get; init; }

    /// <summary>Gets the periods the cabin is dimmed; the lights are up outside them.</summary>
    public required IReadOnlyList<DimmedPeriod> LightingPlan { get; init; }

    /// <summary>Gets the crew count, their zones and the cart spans.</summary>
    public required CrewAssignment Crew { get; init; }

    /// <summary>Gets the phase timeline.</summary>
    public required FlightTimeline Timeline { get; init; }
}

/// <summary>The systems a scenario may switch off, each of which the flight must still run without (R24).</summary>
/// <param name="Needs">Whether passenger needs run.</param>
/// <param name="Events">Whether cabin events run.</param>
/// <param name="Contagion">Whether Unease contagion, the calming source and witnessing run.</param>
/// <param name="Thoughts">Whether passenger thoughts are generated.</param>
public sealed record SystemSwitches(bool Needs, bool Events, bool Contagion, bool Thoughts);

/// <summary>What a service round serves.</summary>
public enum RoundKind
{
    /// <summary>A drinks round.</summary>
    Drinks,

    /// <summary>A meal service.</summary>
    Meal,
}

/// <summary>The way the carts work through their spans.</summary>
public enum ServiceDirection
{
    /// <summary>From the span's front row aft.</summary>
    FrontToBack,

    /// <summary>From the span's back row forward.</summary>
    BackToFront,
}

/// <summary>One round of the service plan.</summary>
/// <param name="Kind">What the round serves.</param>
/// <param name="StartMinutes">Sim minutes after the seatbelt sign first goes off after takeoff.</param>
/// <param name="Direction">The way the carts work.</param>
public sealed record ServiceRound(RoundKind Kind, double StartMinutes, ServiceDirection Direction);

/// <summary>A period the cabin is dimmed, in sim minutes after the seatbelt sign first goes off after takeoff.</summary>
/// <param name="FromMinutes">When the lights go down.</param>
/// <param name="ToMinutes">When they come up.</param>
public sealed record DimmedPeriod(double FromMinutes, double ToMinutes);

/// <summary>The crew lever: how many crew fly, the zones they cover and the carts they run.</summary>
/// <param name="Count">The number of crew.</param>
/// <param name="Zones">The zones; a zone's first crew member is its lead.</param>
/// <param name="Carts">The carts, each a span of rows and its two crew.</param>
public sealed record CrewAssignment(int Count, IReadOnlyList<CrewZone> Zones, IReadOnlyList<CartSpan> Carts);

/// <summary>A zone: a set of rows and stations and the crew who cover them.</summary>
/// <param name="Id">The zone's id.</param>
/// <param name="FirstRow">The zone's first row.</param>
/// <param name="LastRow">The zone's last row.</param>
/// <param name="Stations">The fixture ids of the doors, lavs and galleys in the zone.</param>
/// <param name="Crew">The crew ids covering the zone, lead first.</param>
public sealed record CrewZone(string Id, int FirstRow, int LastRow, IReadOnlyList<string> Stations, IReadOnlyList<string> Crew);

/// <summary>A cart's span of rows and the crew who push it.</summary>
/// <param name="FirstRow">The span's first row.</param>
/// <param name="LastRow">The span's last row.</param>
/// <param name="Crew">The crew ids on the cart.</param>
public sealed record CartSpan(int FirstRow, int LastRow, IReadOnlyList<string> Crew);

/// <summary>The planned phase timeline, anchored at the actual boarding start.</summary>
/// <param name="BoardingStartLocalMinutes">Boarding start as minutes after midnight, origin-local.</param>
/// <param name="Entries">The stage and seatbelt-sign changes, in time order.</param>
public sealed record FlightTimeline(int BoardingStartLocalMinutes, IReadOnlyList<TimelineEntry> Entries);

/// <summary>One point of the timeline: from this time the flight is in this stage with the sign as given.</summary>
/// <param name="AtMinutes">Sim minutes from boarding start; negative before it.</param>
/// <param name="Stage">The stage.</param>
/// <param name="SeatbeltSign">Whether the seatbelt sign is on.</param>
public sealed record TimelineEntry(int AtMinutes, FlightStage Stage, bool SeatbeltSign);
