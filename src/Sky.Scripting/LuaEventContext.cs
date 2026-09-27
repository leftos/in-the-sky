using Lua;
using Sky.Engine.Needs;
using Sky.Engine.Passengers;
using Sky.Engine.Ports;

namespace Sky.Scripting;

/// <summary>
/// The one adapter an event trigger reads the cabin through, as <c>ctx</c>: one instance per flight, pointed at the
/// context of the check before each trigger call. Every member is a read. Passengers are named by id; a list is read
/// as a count and a 1-based index, so a trigger allocates nothing to read it. A per-passenger read of an id that is not
/// aboard, or an index outside its list, is nil.
/// </summary>
/// <remarks>
/// <c>ctx</c> is readable only while a trigger runs: a module that keeps it, in its facts or an upvalue, and reads it
/// from another function raises a Lua error, which disables the module, rather than reading a later check's cabin.
/// </remarks>
[LuaObject]
internal sealed partial class LuaEventContext
{
    /// <summary>The Lua name of each age band, indexed by <see cref="AgeBand"/>.</summary>
    internal static readonly string[] AgeBandNames = ["adult", "child"];

    /// <summary>The Lua name of each cabin class, indexed by <see cref="SeatClass"/>.</summary>
    internal static readonly string[] SeatClassNames = ["business", "economy"];

    private const string NotLive = "ctx is readable only during trigger";

    private static readonly EventContext Empty = new()
    {
        Stage = default,
        SeatbeltOn = false,
        Turbulence = default,
        ServiceRoundRunning = false,
        LongestTaskWaitMinutes = 0.0,
        BoardingSeatedShare = 0.0,
        FreeSeats = [],
        Passengers = [],
    };

    private readonly LuaHost host;
    private readonly string[] traitNames;
    private readonly Dictionary<int, int> indexById = [];
    private EventContext context = Empty;

    /// <summary>Creates the adapter over the flight's trait names, which it shares with the activity adapter.</summary>
    /// <param name="host">The host whose state a read outside a trigger raises its Lua error in.</param>
    /// <param name="traitNames">One trait name per id, indexed by <see cref="TraitId.Value"/>.</param>
    internal LuaEventContext(LuaHost host, string[] traitNames)
    {
        this.host = host;
        this.traitNames = traitNames;
    }

    /// <summary>Gets or sets whether a trigger is running, the only time a module may read the adapter.</summary>
    internal bool IsLive { get; set; }

    /// <summary>Gets the stage of the flight as the kebab-case id events name it by.</summary>
    [LuaMember("stage")]
    public string Stage => LuaPassengerFacts.StageNames[(int)Current().Stage];

    /// <summary>Gets whether the seatbelt sign is lit.</summary>
    [LuaMember("seatbelt_sign")]
    public bool SeatbeltSign => Current().SeatbeltOn;

    /// <summary>Gets how rough the air is.</summary>
    [LuaMember("turbulence")]
    public string TurbulenceName => LuaPassengerFacts.TurbulenceNames[(int)Current().Turbulence];

    /// <summary>Gets whether a service round is running.</summary>
    [LuaMember("service_round_running")]
    public bool ServiceRoundRunning => Current().ServiceRoundRunning;

    /// <summary>Gets the longest wait, in sim minutes, of any task on the task board now.</summary>
    [LuaMember("longest_task_wait_minutes")]
    public double LongestTaskWaitMinutes => Current().LongestTaskWaitMinutes;

    /// <summary>Gets the share of the manifest seated, in [0, 1].</summary>
    [LuaMember("boarding_seated_share")]
    public double BoardingSeatedShare => Current().BoardingSeatedShare;

    /// <summary>Gets the number of passengers aboard.</summary>
    [LuaMember("passenger_count")]
    public double PassengerCount => Current().Passengers.Count;

    /// <summary>Gets the number of free seats.</summary>
    [LuaMember("free_seat_count")]
    public double FreeSeatCount => Current().FreeSeats.Count;

    /// <summary>Gets the id of the passenger at a 1-based index of the passengers aboard.</summary>
    /// <param name="index">The 1-based index.</param>
    /// <returns>The id, or nil outside the list.</returns>
    [LuaMember("passenger")]
    public LuaValue PassengerAt(double index)
    {
        IReadOnlyList<EventPassenger> passengers = Current().Passengers;
        return TryIndex(index, passengers.Count, out int at) ? passengers[at].Id : LuaValue.Nil;
    }

    /// <summary>Gets the label of the free seat at a 1-based index.</summary>
    /// <param name="index">The 1-based index.</param>
    /// <returns>The label, or nil outside the list.</returns>
    [LuaMember("free_seat")]
    public LuaValue FreeSeatLabel(double index) => TryFreeSeat(index, out FreeSeat seat) ? seat.Label : LuaValue.Nil;

    /// <summary>Gets the cabin class of the free seat at a 1-based index.</summary>
    /// <param name="index">The 1-based index.</param>
    /// <returns><c>business</c> or <c>economy</c>, or nil outside the list.</returns>
    [LuaMember("free_seat_class")]
    public LuaValue FreeSeatClass(double index) => TryFreeSeat(index, out FreeSeat seat) ? SeatClassNames[(int)seat.Class] : LuaValue.Nil;

    /// <summary>Gets whether a seat side by side with the free seat at a 1-based index is free too.</summary>
    /// <param name="index">The 1-based index.</param>
    /// <returns>The flag, or nil outside the list.</returns>
    [LuaMember("free_seat_has_free_neighbour")]
    public LuaValue FreeSeatHasFreeNeighbour(double index) => TryFreeSeat(index, out FreeSeat seat) ? seat.HasFreeNeighbour : LuaValue.Nil;

    /// <summary>Gets one of a passenger's five needs by its lowercase name.</summary>
    /// <param name="id">The passenger's id.</param>
    /// <param name="name">The need's name: <c>refreshment</c>, <c>bladder</c>, <c>rest</c>, <c>unease</c> or <c>boredom</c>.</param>
    /// <returns>The need's value, or nil when the passenger is not aboard.</returns>
    /// <exception cref="LuaRuntimeException">The name is not a need's.</exception>
    [LuaMember("need")]
    public LuaValue NeedOf(double id, string name)
    {
        EventContext current = Current();
        int need = Array.IndexOf(ConsequenceReader.NeedNames, name);
        if (need < 0)
        {
            throw new LuaRuntimeException(host.State, $"need: '{name}' is not a need; the needs are refreshment, bladder, rest, unease and boredom");
        }

        return TryFind(current, id, out EventPassenger passenger) ? passenger.Needs[(Need)need] : LuaValue.Nil;
    }

    /// <summary>Reports whether a passenger carries the trait with this content name.</summary>
    /// <param name="id">The passenger's id.</param>
    /// <param name="name">The trait's content name.</param>
    /// <returns>The flag, or nil when the passenger is not aboard.</returns>
    /// <exception cref="LuaRuntimeException">The name is not in the flight's trait table.</exception>
    [LuaMember("has_trait")]
    public LuaValue HasTrait(double id, string name)
    {
        EventContext current = Current();
        int trait = Array.IndexOf(traitNames, name);
        if (trait < 0)
        {
            throw new LuaRuntimeException(host.State, $"has_trait: '{name}' is not a trait of this flight's trait table");
        }

        return TryFind(current, id, out EventPassenger passenger) ? Carries(passenger, trait) : LuaValue.Nil;
    }

    /// <summary>Gets the id of a passenger's group (booking).</summary>
    /// <param name="id">The passenger's id.</param>
    /// <returns>The group id, or nil when the passenger is not aboard.</returns>
    [LuaMember("group")]
    public LuaValue GroupOf(double id) => TryFind(id, out EventPassenger passenger) ? passenger.GroupId : LuaValue.Nil;

    /// <summary>Gets how many other members a passenger's group has.</summary>
    /// <param name="id">The passenger's id.</param>
    /// <returns>The count, or nil when the passenger is not aboard.</returns>
    [LuaMember("group_member_count")]
    public LuaValue GroupMemberCount(double id) => TryFind(id, out EventPassenger passenger) ? passenger.GroupMembers.Count : LuaValue.Nil;

    /// <summary>Gets the id of another member of a passenger's group, at a 1-based index.</summary>
    /// <param name="id">The passenger's id.</param>
    /// <param name="index">The 1-based index.</param>
    /// <returns>The member's id, or nil when the passenger is not aboard or the index is outside the list.</returns>
    [LuaMember("group_member")]
    public LuaValue GroupMember(double id, double index) =>
        TryFind(id, out EventPassenger passenger) ? Item(passenger.GroupMembers, index) : LuaValue.Nil;

    /// <summary>Gets a passenger's age band.</summary>
    /// <param name="id">The passenger's id.</param>
    /// <returns><c>adult</c> or <c>child</c>, or nil when the passenger is not aboard.</returns>
    [LuaMember("age_band")]
    public LuaValue AgeBandOf(double id) => TryFind(id, out EventPassenger passenger) ? AgeBandNames[(int)passenger.AgeBand] : LuaValue.Nil;

    /// <summary>Gets the label of a passenger's seat.</summary>
    /// <param name="id">The passenger's id.</param>
    /// <returns>The label, or nil when the passenger is not aboard.</returns>
    [LuaMember("seat")]
    public LuaValue SeatOf(double id) => TryFind(id, out EventPassenger passenger) ? passenger.Seat : LuaValue.Nil;

    /// <summary>Gets how many neighbours (adjacent and across the aisle) a passenger has.</summary>
    /// <param name="id">The passenger's id.</param>
    /// <returns>The count, or nil when the passenger is not aboard.</returns>
    [LuaMember("neighbour_count")]
    public LuaValue NeighbourCount(double id) => TryFind(id, out EventPassenger passenger) ? passenger.Neighbours.Count : LuaValue.Nil;

    /// <summary>Gets the id of one of a passenger's neighbours, at a 1-based index.</summary>
    /// <param name="id">The passenger's id.</param>
    /// <param name="index">The 1-based index.</param>
    /// <returns>The neighbour's id, or nil when the passenger is not aboard or the index is outside the list.</returns>
    [LuaMember("neighbour")]
    public LuaValue Neighbour(double id, double index) =>
        TryFind(id, out EventPassenger passenger) ? Item(passenger.Neighbours, index) : LuaValue.Nil;

    /// <summary>Gets the id of the passenger in the seat in front of a passenger.</summary>
    /// <param name="id">The passenger's id.</param>
    /// <returns>The id, or nil when that seat is empty or the passenger is not aboard.</returns>
    [LuaMember("front")]
    public LuaValue Front(double id) => TryFind(id, out EventPassenger passenger) && passenger.FrontPassenger is int front ? front : LuaValue.Nil;

    /// <summary>Gets whether a passenger is asleep.</summary>
    /// <param name="id">The passenger's id.</param>
    /// <returns>The flag, or nil when the passenger is not aboard.</returns>
    [LuaMember("asleep")]
    public LuaValue Asleep(double id) => TryFind(id, out EventPassenger passenger) ? passenger.Asleep : LuaValue.Nil;

    /// <summary>Gets whether a passenger is in their seat.</summary>
    /// <param name="id">The passenger's id.</param>
    /// <returns>The flag, or nil when the passenger is not aboard.</returns>
    [LuaMember("seated")]
    public LuaValue Seated(double id) => TryFind(id, out EventPassenger passenger) ? passenger.Seated : LuaValue.Nil;

    /// <summary>Points the adapter at the context of the check whose trigger call is next, once it is known renderable.</summary>
    /// <param name="ctx">The context the next trigger call reads.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="ctx"/> holds a null list, a stage, turbulence level, age band or cabin class that is not defined, a
    /// trait outside the trait table, or two passengers with one id. The message names the index.
    /// </exception>
    internal void SetContext(in EventContext ctx)
    {
        // The id map and the context change together or not at all, so a refused context leaves no stale index behind.
        context = Empty;
        indexById.Clear();
        RequireRenderableFlight(ctx);
        for (int index = 0; index < ctx.Passengers.Count; index++)
        {
            RequireRenderablePassenger(ctx, index);
        }

        for (int index = 0; index < ctx.Passengers.Count; index++)
        {
            EventPassenger passenger = ctx.Passengers[index];
            if (!indexById.TryAdd(passenger.Id, index))
            {
                indexById.Clear();
                throw new ArgumentException($"Passenger {index} repeats the id {passenger.Id}; passenger ids are unique.", nameof(ctx));
            }
        }

        context = ctx;
    }

    /// <summary>Reports whether a Lua value is the id of a passenger aboard in the context last set; a host-side read.</summary>
    /// <param name="value">The value.</param>
    /// <param name="id">The id, when it is one.</param>
    /// <returns><see langword="true"/> when the value is a whole number naming a passenger aboard.</returns>
    internal bool IsPassengerId(LuaValue value, out int id)
    {
        id = 0;
        if (value.Type != LuaValueType.Number || !TryFind(context, value.Read<double>(), out EventPassenger passenger))
        {
            return false;
        }

        id = passenger.Id;
        return true;
    }

    private static void RequireRenderableFlight(in EventContext ctx)
    {
        if (ctx.Passengers is null || ctx.FreeSeats is null)
        {
            throw new ArgumentException(
                "Passengers or FreeSeats is null; a context with nobody aboard or no free seat holds an empty list.",
                nameof(ctx)
            );
        }

        if (!Enum.IsDefined(ctx.Stage))
        {
            throw new ArgumentException($"Stage {(int)ctx.Stage} is not a defined stage.", nameof(ctx));
        }

        if (!Enum.IsDefined(ctx.Turbulence))
        {
            throw new ArgumentException($"Turbulence {(int)ctx.Turbulence} is not a defined level.", nameof(ctx));
        }

        for (int index = 0; index < ctx.FreeSeats.Count; index++)
        {
            FreeSeat seat = ctx.FreeSeats[index];
            if (seat.Label is null || !Enum.IsDefined(seat.Class))
            {
                throw new ArgumentException($"Free seat {index} has a null label or a class that is not defined.", nameof(ctx));
            }
        }
    }

    private void RequireRenderablePassenger(in EventContext ctx, int index)
    {
        EventPassenger passenger = ctx.Passengers[index];
        if (passenger.Needs is null || passenger.Seat is null || passenger.GroupMembers is null || passenger.Neighbours is null)
        {
            throw new ArgumentException($"Passenger {index} has a null Needs, Seat, GroupMembers or Neighbours.", nameof(ctx));
        }

        if (!Enum.IsDefined(passenger.AgeBand))
        {
            throw new ArgumentException($"Passenger {index} has age band {(int)passenger.AgeBand}, which is not defined.", nameof(ctx));
        }

        foreach (TraitId trait in passenger.Traits.Span)
        {
            if ((uint)trait.Value >= (uint)traitNames.Length)
            {
                throw new ArgumentException(
                    $"Passenger {index} has trait {trait.Value}, outside the trait table of {traitNames.Length}.",
                    nameof(ctx)
                );
            }
        }
    }

    private EventContext Current() => IsLive ? context : throw new LuaRuntimeException(host.State, NotLive);

    private static bool Carries(in EventPassenger passenger, int traitIndex)
    {
        foreach (TraitId trait in passenger.Traits.Span)
        {
            if (trait.Value == traitIndex)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryFreeSeat(double index, out FreeSeat seat)
    {
        IReadOnlyList<FreeSeat> seats = Current().FreeSeats;
        bool found = TryIndex(index, seats.Count, out int at);
        seat = found ? seats[at] : default;
        return found;
    }

    private bool TryFind(double id, out EventPassenger passenger) => TryFind(Current(), id, out passenger);

    private bool TryFind(in EventContext ctx, double id, out EventPassenger passenger)
    {
        if (id >= int.MinValue && id <= int.MaxValue && Math.Floor(id) == id && indexById.TryGetValue((int)id, out int index))
        {
            passenger = ctx.Passengers[index];
            return true;
        }

        passenger = default;
        return false;
    }

    private static LuaValue Item(IReadOnlyList<int> ids, double index) => TryIndex(index, ids.Count, out int at) ? ids[at] : LuaValue.Nil;

    private static bool TryIndex(double luaIndex, int count, out int index)
    {
        bool inside = luaIndex >= 1 && luaIndex <= count && Math.Floor(luaIndex) == luaIndex;
        index = inside ? (int)luaIndex - 1 : -1;
        return inside;
    }
}
