using Sky.Engine.Events;
using Sky.Engine.Flight;
using Sky.Engine.Needs;
using Sky.Engine.Passengers;

namespace Sky.Engine.Ports;

/// <summary>
/// Everything an event trigger reads, exactly the facts of <c>docs/design/events.md</c> section 7: the flight, the task
/// board, boarding progress, the free seats and every passenger aboard. Handed to <see cref="IBehaviorScripts.Trigger"/>
/// by read-only reference and read only during that call.
/// </summary>
public readonly record struct EventContext
{
    /// <summary>Gets the stage of the flight.</summary>
    public required FlightStage Stage { get; init; }

    /// <summary>Gets whether the seatbelt sign is lit.</summary>
    public required bool SeatbeltOn { get; init; }

    /// <summary>Gets how rough the air is.</summary>
    public required Turbulence Turbulence { get; init; }

    /// <summary>Gets whether a service round is running.</summary>
    public required bool ServiceRoundRunning { get; init; }

    /// <summary>Gets the longest wait, in sim minutes, of any task on the task board now; 0 when the board is empty.</summary>
    public required double LongestTaskWaitMinutes { get; init; }

    /// <summary>Gets the share of the manifest seated, in [0, 1].</summary>
    public required double BoardingSeatedShare { get; init; }

    /// <summary>
    /// Gets the unbooked seats, in seat node order: row by row from the front, left to right within a row. A trigger
    /// that picks the first seat that fits depends on this order for a flight to replay the same.
    /// </summary>
    public required IReadOnlyList<FreeSeat> FreeSeats { get; init; }

    /// <summary>
    /// Gets every passenger aboard, each id once, in ascending passenger id. A trigger that picks the first passenger
    /// that fits depends on this order for a flight to replay the same.
    /// </summary>
    public required IReadOnlyList<EventPassenger> Passengers { get; init; }
}

/// <summary>One unbooked seat as a trigger sees it.</summary>
/// <param name="Label">The seat's label, such as <c>23A</c>.</param>
/// <param name="Class">The cabin class the seat belongs to.</param>
/// <param name="HasFreeNeighbour">Whether a seat side by side with this one is free too.</param>
public readonly record struct FreeSeat(string Label, SeatClass Class, bool HasFreeNeighbour);

/// <summary>One passenger aboard as a trigger sees them.</summary>
public readonly record struct EventPassenger
{
    /// <summary>Gets the passenger's id, which the facts and consequence targets name them by.</summary>
    public required int Id { get; init; }

    /// <summary>Gets the passenger's five needs as they stand now; the passenger's own live set, read only during the call.</summary>
    public required NeedSet Needs { get; init; }

    /// <summary>Gets the passenger's traits as a view over their own array.</summary>
    public required ReadOnlyMemory<TraitId> Traits { get; init; }

    /// <summary>Gets the id of the passenger's group (booking).</summary>
    public required int GroupId { get; init; }

    /// <summary>
    /// Gets the ids of the other members of the passenger's group, in ascending passenger id; empty for a passenger
    /// travelling alone.
    /// </summary>
    public required IReadOnlyList<int> GroupMembers { get; init; }

    /// <summary>Gets the passenger's age band.</summary>
    public required AgeBand AgeBand { get; init; }

    /// <summary>Gets the label of the passenger's seat.</summary>
    public required string Seat { get; init; }

    /// <summary>Gets the ids of the passengers in the adjacent seats and across the aisle, in ascending passenger id.</summary>
    public required IReadOnlyList<int> Neighbours { get; init; }

    /// <summary>Gets the id of the passenger in the seat in front, or <see langword="null"/> when that seat is empty or absent.</summary>
    public required int? FrontPassenger { get; init; }

    /// <summary>Gets whether the passenger is asleep.</summary>
    public required bool Asleep { get; init; }

    /// <summary>Gets whether the passenger is in their seat.</summary>
    public required bool Seated { get; init; }
}

/// <summary>
/// What a trigger recorded when its event fired. The facts themselves stay with the scripts; the Engine holds this key
/// to them and hands it back to <see cref="IBehaviorScripts.Describe"/>, <see cref="IBehaviorScripts.Choices"/> and
/// <see cref="IBehaviorScripts.Effects"/> until it calls <see cref="IBehaviorScripts.Release"/>.
/// </summary>
/// <param name="EventId">The id of the event module that fired.</param>
/// <param name="Handle">The scripts' key for the recorded facts; opaque to the Engine.</param>
/// <param name="Subject">The passenger the facts name as their subject, or <see langword="null"/> when they name none.</param>
public sealed record EventFacts(string EventId, int Handle, int? Subject);

/// <summary>One choice an event offers (<c>docs/design/events.md</c> section 1).</summary>
/// <param name="Id">The choice's id, unique within the event; <c>leave</c> is the crew-free choice.</param>
/// <param name="Label">The words a player reads.</param>
/// <param name="NeedsCrew">Whether the choice becomes a task on the task board.</param>
/// <param name="Minutes">The sim minutes the crew work takes, at least 0; 0 for a choice with no crew.</param>
/// <param name="Quality">How good the choice is for these facts, in [0, 1], which auto-resolve reads (OD1).</param>
public sealed record EventChoice(string Id, string Label, bool NeedsCrew, double Minutes, double Quality);

/// <summary>How an <see cref="EventTarget"/> names who a consequence lands on.</summary>
public enum EventTargetKind
{
    /// <summary>The passenger the facts name as their subject.</summary>
    Subject,

    /// <summary>The subject's neighbours, leaving out any passenger the facts name.</summary>
    Neighbours,

    /// <summary>
    /// One passenger, by id. The scripts check only that the id is a whole number; the host drops, and journals, a
    /// consequence whose passenger is not aboard when it lands.
    /// </summary>
    Passenger,
}

/// <summary>
/// Who a consequence lands on, a selector the host resolves (<c>docs/design/events.md</c> section 1): the subject, the
/// subject's neighbours, or a passenger id the trigger recorded.
/// </summary>
public readonly record struct EventTarget
{
    /// <summary>Gets the target that names the subject.</summary>
    public static EventTarget Subject { get; } = new() { Kind = EventTargetKind.Subject };

    /// <summary>Gets the target that names the subject's neighbours.</summary>
    public static EventTarget Neighbours { get; } = new() { Kind = EventTargetKind.Neighbours };

    /// <summary>Gets how the target names who it lands on.</summary>
    public EventTargetKind Kind { get; private init; }

    /// <summary>Gets the passenger's id when <see cref="Kind"/> is <see cref="EventTargetKind.Passenger"/>; 0 otherwise.</summary>
    public int PassengerId { get; private init; }

    /// <summary>Creates the target that names one passenger.</summary>
    /// <param name="id">The passenger's id.</param>
    /// <returns>The target.</returns>
    public static EventTarget Passenger(int id) => new() { Kind = EventTargetKind.Passenger, PassengerId = id };
}

/// <summary>
/// One consequence of a chosen branch, landing <see cref="OffsetTicks"/> after its clock starts (<c>docs/design/events.md</c>
/// section 5). Exactly one of the four kinds of section 1: <see cref="NeedConsequence"/>, <see cref="IncidentConsequence"/>,
/// a seat move (<see cref="SeatSwapConsequence"/> or <see cref="SeatMoveConsequence"/>), or <see cref="LineConsequence"/>.
/// </summary>
/// <param name="OffsetTicks">The ticks from the start of the consequence's clock to the tick it lands on.</param>
public abstract record DelayedConsequence(long OffsetTicks);

/// <summary>The need kind (<c>docs/design/events.md</c> section 1): moves one need of each target by a delta, clamped to 0 to 100.</summary>
/// <param name="OffsetTicks">The ticks from the start of the consequence's clock to the tick it lands on.</param>
/// <param name="Target">Who the change lands on.</param>
/// <param name="Need">The need that moves.</param>
/// <param name="Delta">The change, positive or negative.</param>
public sealed record NeedConsequence(long OffsetTicks, EventTarget Target, Need Need, double Delta) : DelayedConsequence(OffsetTicks);

/// <summary>The incident kind (<c>docs/design/events.md</c> section 1): raises an incident of that kind on the target.</summary>
/// <param name="OffsetTicks">The ticks from the start of the consequence's clock to the tick it lands on.</param>
/// <param name="Target">Who raises the incident.</param>
/// <param name="Incident">The kind of incident.</param>
public sealed record IncidentConsequence(long OffsetTicks, EventTarget Target, IncidentKind Incident) : DelayedConsequence(OffsetTicks);

/// <summary>
/// The seat-move kind as a swap (<c>docs/design/events.md</c> section 1): the two passengers trade seats, once the host
/// has checked both are aboard and neither is mid-walk. Each side names one passenger, never the neighbours.
/// </summary>
/// <param name="OffsetTicks">The ticks from the start of the consequence's clock to the tick it lands on.</param>
/// <param name="First">One passenger of the swap.</param>
/// <param name="Second">The other passenger of the swap.</param>
public sealed record SeatSwapConsequence(long OffsetTicks, EventTarget First, EventTarget Second) : DelayedConsequence(OffsetTicks);

/// <summary>
/// The seat-move kind as a move (<c>docs/design/events.md</c> section 1): one passenger moves to a free seat the facts
/// recorded, once the host has checked the seat is still free and in the same cabin class.
/// </summary>
/// <param name="OffsetTicks">The ticks from the start of the consequence's clock to the tick it lands on.</param>
/// <param name="Target">Who moves.</param>
/// <param name="ToSeat">The label of the seat they move to.</param>
public sealed record SeatMoveConsequence(long OffsetTicks, EventTarget Target, string ToSeat) : DelayedConsequence(OffsetTicks);

/// <summary>
/// The line kind (<c>docs/design/events.md</c> section 1): journals the line as a moment tied to the event, the choice
/// and the target. Changes no state.
/// </summary>
/// <param name="OffsetTicks">The ticks from the start of the consequence's clock to the tick it lands on.</param>
/// <param name="Target">Who the line is about.</param>
/// <param name="Line">The scene line a player reads.</param>
public sealed record LineConsequence(long OffsetTicks, EventTarget Target, string Line) : DelayedConsequence(OffsetTicks);
