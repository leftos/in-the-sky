using Sky.Engine.Cabin;
using Sky.Engine.Execution;
using Sky.Engine.Manifest;
using Sky.Engine.Passengers;
using Sky.Engine.Randomness;

namespace Sky.Engine.Flight;

/// <summary>
/// Boarding and deboarding, by the boarding door: the door fixture with the lowest row index, the first listed among equals.
/// Boarding builds the queue when the boarding stage starts: business bookings first, then economy back to front in zones of
/// <see cref="MovementRules.EconomyZoneRows"/> rows counted from the back row, each booking in the zone of its rearmost seat,
/// bookings in random order within business and within each zone from the <see cref="StreamName"/> stream, a booking's members
/// together in id order. While the flight is in the boarding stage, each tick the queue's next passenger enters the door when it
/// has room, walks to their row's aisle slot, stows, and crosses to their seat; a passenger not in when the stage ends never
/// boards. Deboarding waits until every boarded passenger is seated, then each tick lets the next passenger of every row (the
/// one nearest the aisle first) stand at once, once the previous one of that row has left their seat and the row's aisle slot
/// is empty; the aisle and the door then order them. The passenger retrieves their bag, walks to the door in the deboarding
/// line, and leaves. Rows do not wait for the rows ahead (orchestrator ruling, F3b): standing them strictly front to back
/// would take about 45 minutes on the reference cabin against balance.md section 3.5's target of about 14. A passenger busy
/// with a higher-priority action is passed over until it is free.
/// </summary>
internal sealed class BoardingFlow
{
    /// <summary>The named stream the boarding order is drawn from.</summary>
    public const string StreamName = "boarding";

    private readonly FlightWorld world;
    private readonly PassengerManifest manifest;
    private readonly MovementRules rules;
    private readonly int door;
    private readonly int[] rowSlots;
    private int[] queue = [];
    private int nextEntry;
    private int[][] standOrder = [];
    private int[] nextStand = [];
    private bool[] standing = [];
    private bool aisleClear;

    /// <summary>Prepares the flow; nothing moves until a stage starts.</summary>
    /// <param name="world">The flight, whose graph, paths, passengers, executor and mover are built.</param>
    /// <param name="manifest">The passengers and their bookings.</param>
    /// <param name="rules">The movement numbers.</param>
    /// <exception cref="ArgumentException">The layout has no door, or a booked seat has no aisle slot on its way to the door; the
    /// error names <see cref="FlightSetup.Layout"/>.</exception>
    public BoardingFlow(FlightWorld world, PassengerManifest manifest, MovementRules rules)
    {
        this.world = world;
        this.manifest = manifest;
        this.rules = rules;
        door = world.Graph.FixtureNode(BoardingDoor(world.Layout).Id);
        rowSlots = [.. manifest.Passengers.Select(passenger => RowSlot(world, passenger.SeatNode, door))];
    }

    /// <summary>Gets whether boarding has started.</summary>
    public bool BoardingStarted { get; private set; }

    /// <summary>Gets whether deboarding has started.</summary>
    public bool DeboardingStarted { get; private set; }

    /// <summary>Builds the boarding queue; passengers start entering on the tick it is built.</summary>
    public void StartBoarding()
    {
        queue = BoardingOrder(world.Rng.Stream(StreamName));
        BoardingStarted = true;
    }

    /// <summary>Builds each row's standing order from the passengers aboard, the one nearest the aisle first.</summary>
    public void StartDeboarding()
    {
        NavGraph graph = world.Graph;
        standOrder = new int[world.Layout.Rows.Count][];
        for (int row = 0; row < standOrder.Length; row++)
        {
            standOrder[row] =
            [
                .. world
                    .Passengers.Where(passenger => passenger.IsBoarded && graph.Nodes[passenger.Manifest.SeatNode].RowIndex == row)
                    .OrderBy(passenger => world.Paths.Cost(passenger.Manifest.SeatNode, rowSlots[passenger.Id]))
                    .ThenBy(passenger => passenger.Manifest.SeatNode)
                    .Select(passenger => passenger.Id),
            ];
        }

        nextStand = new int[standOrder.Length];
        standing = new bool[manifest.Passengers.Count];
        DeboardingStarted = true;
    }

    /// <summary>Lets the next passenger in at the door, and stands the passengers whose turn it is; runs before the executor.</summary>
    /// <param name="tick">The tick being run.</param>
    public void Tick(long tick)
    {
        if (BoardingStarted && world.Stages.Current == FlightStage.Boarding)
        {
            TryEnterNext(tick);
        }

        if (!DeboardingStarted || !AisleClear())
        {
            return;
        }

        for (int row = 0; row < standOrder.Length; row++)
        {
            StandNextInRow(row, tick);
        }
    }

    /// <summary>Returns whether every passenger is seated.</summary>
    /// <returns>True when every passenger's state is seated.</returns>
    public bool AllSeated() => world.Passengers.All(passenger => world.Movement.StateOf(passenger.CharacterId) == MoverState.Seated);

    /// <summary>Returns whether every passenger who boarded has left the aircraft, so that nobody is aboard.</summary>
    /// <returns>True when no passenger is aboard.</returns>
    public bool AllOff() => !world.Passengers.Any(passenger => passenger.IsBoarded);

    /// <summary>The door passengers board and leave by: the door fixture with the lowest row index, the first listed among equals.</summary>
    private static CabinFixture BoardingDoor(CabinLayout layout) =>
        layout.Fixtures.Where(fixture => fixture.Kind == FixtureKind.Door).OrderBy(fixture => fixture.RowIndex).FirstOrDefault()
        ?? throw LayoutRefusal($"Layout '{layout.Id}' has no door to board by.", nameof(FlightSetup.Layout));

    /// <summary>The aisle slot of a seat's row: the first aisle slot on the cheapest walk from the seat to the door.</summary>
    private static int RowSlot(FlightWorld world, int seat, int door)
    {
        int node = seat;
        while (world.Graph.Nodes[node].Kind != NodeKind.AisleSlot)
        {
            node = world.Paths.NextHop(node, door);
            if (node < 0 || node == door)
            {
                throw LayoutRefusal($"Seat node {seat} has no aisle slot on its way to the door.", nameof(FlightSetup.Layout));
            }
        }

        return node;
    }

    /// <summary>A refusal of the setup's layout, naming the setup field.</summary>
    private static ArgumentException LayoutRefusal(string message, string field) => new(message, field);

    private static void Shuffle(List<Booking> bookings, SimRandom stream)
    {
        for (int last = bookings.Count - 1; last > 0; last--)
        {
            int pick = stream.NextInt(last + 1);
            (bookings[last], bookings[pick]) = (bookings[pick], bookings[last]);
        }
    }

    private static IEnumerable<int> Members(Booking booking) => booking.PassengerIds.Order();

    /// <summary>The passenger ids in boarding order: business bookings shuffled, then each economy zone from the back, shuffled.</summary>
    private int[] BoardingOrder(SimRandom stream)
    {
        Booking[] bookings = [.. manifest.Bookings.OrderBy(booking => booking.Id)];
        List<Booking> business = [.. bookings.Where(IsBusiness)];
        Shuffle(business, stream);
        List<Booking>[] zones = EconomyZones(bookings.Where(booking => !IsBusiness(booking)));
        List<int> order = [.. business.SelectMany(Members)];
        foreach (List<Booking> zone in zones)
        {
            Shuffle(zone, stream);
            order.AddRange(zone.SelectMany(Members));
        }

        return [.. order];
    }

    /// <summary>Economy bookings by zone, the back zone first: a booking's zone is its rearmost seat's row, counted from the back row.</summary>
    private List<Booking>[] EconomyZones(IEnumerable<Booking> economy)
    {
        int lastRow = world.Layout.Rows.Count - 1;
        var zones = new List<Booking>[(lastRow / rules.EconomyZoneRows) + 1];
        for (int zone = 0; zone < zones.Length; zone++)
        {
            zones[zone] = [];
        }

        foreach (Booking booking in economy)
        {
            int rearmost = booking.PassengerIds.Max(id => world.Graph.Nodes[manifest.Passengers[id].SeatNode].RowIndex);
            zones[(lastRow - rearmost) / rules.EconomyZoneRows].Add(booking);
        }

        return zones;
    }

    private bool IsBusiness(Booking booking) => booking.PassengerIds.Any(id => manifest.Passengers[id].SeatClass == SeatClass.Business);

    /// <summary>Whether no boarded passenger is still on their way to their seat; once true it stays true, since boarding has ended.</summary>
    private bool AisleClear()
    {
        aisleClear = aisleClear || world.Passengers.All(passenger => !passenger.IsBoarded || IsSeated(passenger));
        return aisleClear;
    }

    private bool IsSeated(Passenger passenger) => world.Movement.StateOf(passenger.CharacterId) == MoverState.Seated;

    /// <summary>Whether a character's current action, if any, gives way to a movement action.</summary>
    private bool IsFree(int character) => world.Executor.ActionOf(character) is not { } action || action.Priority < Movement.ActionPriority;

    private void TryEnterNext(long tick)
    {
        if (nextEntry >= queue.Length)
        {
            return;
        }

        Passenger passenger = world.Passengers[queue[nextEntry]];
        int character = passenger.CharacterId;
        if (!IsFree(character) || !world.Movement.TryEnter(character, door))
        {
            return;
        }

        nextEntry++;
        Movement mover = world.Movement;
        SimRandom stream = world.PassengerStream(passenger.Id);
        Start(
            character,
            [
                () => mover.Walk(character, rowSlots[passenger.Id], queueWhenBlocked: false),
                () => mover.Hold(character, MoverState.Stowing, rules.StowTicks, stream),
                () => mover.Cross(character, passenger.Manifest.SeatNode, MoverState.Seated),
            ],
            tick
        );
    }

    private void StandNextInRow(int row, long tick)
    {
        int[] order = standOrder[row];
        while (nextStand[row] < order.Length)
        {
            Passenger passenger = world.Passengers[order[nextStand[row]]];
            if (!standing[passenger.Id])
            {
                TryStand(passenger, tick);
                return;
            }

            if (passenger.Node == passenger.Manifest.SeatNode)
            {
                return;
            }

            nextStand[row]++;
        }
    }

    private void TryStand(Passenger passenger, long tick)
    {
        int character = passenger.CharacterId;
        int slot = rowSlots[passenger.Id];
        bool ready = IsSeated(passenger) && IsFree(character) && world.Occupancy.HolderCount(slot) == 0;
        if (!ready)
        {
            return;
        }

        standing[passenger.Id] = true;
        Movement mover = world.Movement;
        SimRandom stream = world.PassengerStream(passenger.Id);
        Start(
            character,
            [
                () => mover.Cross(character, slot, MoverState.Standing),
                () => mover.Hold(character, MoverState.Retrieving, rules.RetrievalTicks, stream),
                () => mover.Walk(character, door, queueWhenBlocked: true),
                () => mover.Exit(character),
            ],
            tick
        );
    }

    /// <summary>Starts a checked-free character's movement; a refusal here means <see cref="IsFree"/> and the executor disagree.</summary>
    private void Start(int character, IReadOnlyList<Func<CharacterAction>> steps, long tick)
    {
        if (!world.Executor.TryStart(character, new ActionChain(steps), tick))
        {
            throw new InvalidOperationException($"Character {character} was free to move, but the executor refused its movement.");
        }
    }
}

/// <summary>A stage handler that runs a callback on entry.</summary>
/// <param name="start">The callback, given the tick the stage was entered on.</param>
internal sealed class StageHandler(Action<long> start) : IStageHandler
{
    /// <inheritdoc/>
    public void Start(long tick) => start(tick);
}
