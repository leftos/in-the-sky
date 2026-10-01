using Sky.Engine.Cabin;
using Sky.Engine.Execution;
using Sky.Engine.Flight;
using Sky.Engine.Manifest;
using Sky.Engine.Needs;
using Sky.Engine.Passengers;
using Sky.Engine.Ports;
using Sky.Engine.Randomness;
using Sky.Engine.Tests.Cabin;
using Sky.Engine.Tests.Fakes;
using Sky.Engine.Tests.Passengers;
using static Sky.Engine.Tests.Flight.TestFlights;

namespace Sky.Engine.Tests.Flight;

/// <summary>Proves boarding and deboarding: the order passengers enter in, the blocking in the aisle, and that everyone sits and leaves.</summary>
public sealed class BoardingFlowTests
{
    private const ulong Seed = 9;

    /// <summary>A priority above every movement action's.</summary>
    private const int BusyPriority = 100;

    private static readonly Dictionary<string, Func<MovementRules, MovementRules>> BadRules = new()
    {
        ["StowReversed"] = rules => rules with { StowTicks = new IntRange(9, 8) },
        ["StowNegative"] = rules => rules with { StowTicks = new IntRange(-1, 8) },
        ["RetrievalReversed"] = rules => rules with { RetrievalTicks = new IntRange(9, 8) },
        ["SqueezeNegative"] = rules => rules with { SqueezeExtraTicks = -1 },
        ["CrossNegative"] = rules => rules with { OccupiedSeatCrossTicks = -1 },
        ["ZoneRowsZero"] = rules => rules with { EconomyZoneRows = 0 },
    };

    /// <summary>Every passenger's node is their seat on the tick all are seated, and that tick is recorded.</summary>
    [Fact]
    public void EveryPassengerIsSeatedByAllSeatedTick()
    {
        var flight = new FlightWorld(Setup(FullManifest(), Seed));

        RunUntil(flight, world => world.AllSeatedTick is not null);

        Assert.NotNull(flight.AllSeatedTick);
        Assert.All(flight.Passengers, passenger => Assert.Equal(passenger.Manifest.SeatNode, passenger.Node));
    }

    /// <summary>After deboarding every passenger is off the aircraft and the tick the last one left is recorded.</summary>
    [Fact]
    public void EveryPassengerIsOffAtTheEnd()
    {
        var flight = new FlightWorld(Setup(FullManifest(), Seed));

        RunUntil(flight, world => world.AllOffTick is not null);

        Assert.True(flight.AllOffTick >= DeboardingTick, $"The last passenger left at {flight.AllOffTick}, before deboarding.");
        Assert.All(
            flight.Passengers,
            passenger =>
            {
                Assert.True(passenger.IsOff);
                Assert.False(passenger.IsBoarded);
                Assert.Null(passenger.Node);
            }
        );
    }

    /// <summary>
    /// On every tick of a whole flight each boarded passenger stands on a node of the graph and has arrived there, and every
    /// holder the occupancy lists is a boarded passenger: at the node when it has arrived, elsewhere when it only reserved it.
    /// </summary>
    [Fact]
    public void NoCharacterIsEverOffTheNavGraph()
    {
        var flight = new FlightWorld(Setup(FullManifest(), Seed));

        while (flight.AllOffTick is null)
        {
            Assert.True(flight.Tick < TickLimit, "The flight never emptied.");
            flight.Step(1);
            AssertOnTheGraph(flight);
        }
    }

    /// <summary>
    /// A feed that jumps to deboarding with passengers still queued stops boarding: those not yet in never board, everyone who
    /// boarded gets off without a deadlock, and nobody is ever all seated.
    /// </summary>
    [Fact]
    public void AFeedJumpToDeboardingStopsBoarding()
    {
        var flight = new FlightWorld(Setup(FullManifest(), Seed) with { Feed = new StagedFeed(BoardingTick, 150) });

        RunUntil(flight, world => world.AllOffTick is not null);

        Assert.Null(flight.AllSeatedTick);
        Assert.Contains(flight.Passengers, passenger => passenger.IsOff);
        Assert.Contains(flight.Passengers, passenger => !passenger.IsOff);
        Assert.All(flight.Passengers, passenger => Assert.False(passenger.IsBoarded));
    }

    /// <summary>While the row-5 passenger stows in row 5's aisle slot, the row-9 passenger behind them never enters that slot.</summary>
    [Fact]
    public void AStowingPassengerBlocksTheOneBehind()
    {
        NavGraph graph = Graph();
        PassengerManifest manifest = ManifestOf(
            graph,
            [
                [SeatIn(graph, 5, 0), SeatIn(graph, 9, 0)],
            ]
        );
        var flight = new FlightWorld(Setup(manifest, Seed));
        Passenger front = flight.Passengers[0];
        Passenger back = flight.Passengers[1];
        int rowFiveSlot = graph.AisleSlot(5, 0);
        int rowFourSlot = graph.AisleSlot(4, 0);
        int stowingTicks = 0;
        bool waitedBehind = false;

        while (flight.AllSeatedTick is null)
        {
            Assert.True(flight.Tick < TickLimit, "Boarding never finished.");
            flight.Step(1);
            if (flight.Movement.StateOf(front.CharacterId) == MoverState.Stowing)
            {
                stowingTicks++;
                waitedBehind |= flight.Occupancy.HasArrived(rowFourSlot, back.CharacterId);
                Assert.False(
                    flight.Occupancy.Holds(rowFiveSlot, back.CharacterId),
                    $"Tick {flight.Tick - 1}: the passenger behind entered the stower's slot."
                );
            }
        }

        Assert.True(stowingTicks > 0, "The row-5 passenger never stowed.");
        Assert.True(waitedBehind, "The row-9 passenger never reached the slot behind the stower.");
    }

    /// <summary>With the stow draw pinned to one tick, the stower reads as stowing at the end of the draw tick.</summary>
    [Fact]
    public void AStowDrawOfOneReadsAsStowingForOneTick()
    {
        FlightWorld flight = StowPinnedFlight(1);
        Passenger stower = flight.Passengers[0];
        int stowingTicks = 0;
        while (flight.AllSeatedTick is null)
        {
            Assert.True(flight.Tick < TickLimit, "Boarding never finished.");
            flight.Step(1);
            if (flight.Movement.StateOf(stower.CharacterId) == MoverState.Stowing)
            {
                stowingTicks++;
            }
        }

        Assert.Equal(1, stowingTicks);
    }

    /// <summary>With the stow draw pinned to zero ticks, the stower never reads as stowing, and boarding still finishes.</summary>
    [Fact]
    public void AStowDrawOfZeroHoldsNoTick()
    {
        List<int> stowing = StowingTickEnds(StowerTickEnds(StowPinnedFlight(0)));

        Assert.Empty(stowing);
    }

    /// <summary>
    /// With the stow draw pinned to five ticks, the stower reads as stowing at the end of five consecutive ticks, and has left
    /// the state on the tick after.
    /// </summary>
    [Fact]
    public void AStowDrawOfFiveHoldsTheStowingStateForFiveTicks()
    {
        List<MoverState> tickEnds = StowerTickEnds(StowPinnedFlight(5));
        List<int> stowing = StowingTickEnds(tickEnds);
        MoverState after = tickEnds[stowing[0] + 5];

        Assert.True(
            after is MoverState.Standing or MoverState.Walking or MoverState.Seated,
            $"The tick after the fifth stowing tick reads as {after}."
        );
        Assert.Equal(5, stowing.Count);
        Assert.Equal(stowing[0] + 4, stowing[^1]);
    }

    /// <summary>A crew member walking up the aisle passes a stowing passenger by squeeze, arriving the squeeze's ticks later.</summary>
    [Fact]
    public void CrewPassAStowingPassenger()
    {
        long unobstructed = CrewArrivalTick(_ => { });
        long past = CrewArrivalTick(flight =>
        {
            int stower = flight.Passengers[0].CharacterId;
            Assert.True(flight.Movement.TryEnter(stower, flight.Graph.AisleSlot(4, 0)));
            CharacterAction stow = flight.Movement.Hold(stower, MoverState.Stowing, new IntRange(1000, 1000), flight.Rng.Stream("test"));
            Assert.True(flight.Executor.TryStart(stower, stow, flight.Tick));
        });

        Assert.Equal(unobstructed + Rules().SqueezeExtraTicks, past);
    }

    /// <summary>A walker behind another going the same way shares no node with it while it walks, so never passes it.</summary>
    [Fact]
    public void AFollowerNeverPassesTheWalkerAhead()
    {
        FlightWorld flight = NeverBoardingFlight();
        Movement mover = flight.Movement;
        int leaderTarget = flight.Graph.AisleSlot(8, 0);
        int followerTarget = flight.Graph.AisleSlot(9, 0);
        Assert.True(mover.TryEnter(0, flight.Graph.AisleSlot(3, 0)));
        Assert.True(mover.TryEnter(1, flight.Graph.AisleSlot(2, 0)));
        Assert.True(flight.Executor.TryStart(0, mover.Walk(0, leaderTarget, queueWhenBlocked: false), flight.Tick));
        Assert.True(flight.Executor.TryStart(1, mover.Walk(1, followerTarget, queueWhenBlocked: false), flight.Tick));

        while (mover.NodeOf(1) != followerTarget)
        {
            Assert.True(flight.Tick < TickLimit, "The follower never arrived.");
            flight.Step(1);
            for (int node = 0; node < flight.Graph.Nodes.Count; node++)
            {
                bool leaderWalking = mover.StateOf(0) == MoverState.Walking;
                bool shared = leaderWalking && flight.Occupancy.Holds(node, 0) && flight.Occupancy.Holds(node, 1);
                Assert.False(shared, $"Tick {flight.Tick - 1}: the follower shares node {node} with the walker ahead.");
            }
        }
    }

    /// <summary>Two crew walking toward each other each squeeze past the other once, each arriving the squeeze's ticks late.</summary>
    [Fact]
    public void OppositeWalkersEachSqueezeOnce()
    {
        (long aftAlone, _) = OppositeWalkArrivals(walkAft: true, walkForward: false);
        (_, long forwardAlone) = OppositeWalkArrivals(walkAft: false, walkForward: true);
        (long aft, long forward) = OppositeWalkArrivals(walkAft: true, walkForward: true);

        int squeeze = Rules().SqueezeExtraTicks;
        Assert.Equal(aftAlone + squeeze, aft);
        Assert.Equal(forwardAlone + squeeze, forward);
    }

    /// <summary>A window passenger crossing their seated aisle neighbour takes the crossing ticks longer than crossing an empty row.</summary>
    [Fact]
    public void CrossingASeatedNeighbourCostsTicks()
    {
        NavGraph graph = Graph();
        int window = SeatIn(graph, 5, 0);
        int aisle = SeatIn(graph, 5, 2);

        long pastNeighbour = WindowCrossTicks(
            ManifestOf(
                graph,
                [
                    [aisle, window],
                ]
            ),
            1
        );
        long emptyRow = WindowCrossTicks(
            ManifestOf(
                graph,
                [
                    [window, aisle],
                ]
            ),
            0
        );

        Assert.Equal(emptyRow + Rules().OccupiedSeatCrossTicks, pastNeighbour);
    }

    /// <summary>In every row the passengers leave their seats nearest the aisle first, the lower seat node first among equals.</summary>
    [Fact]
    public void TheAisleSeatStandsFirst()
    {
        var flight = new FlightWorld(Setup(FullManifest(), Seed));
        RunUntil(flight, world => world.Tick >= DeboardingTick);
        long[] leftSeat = new long[flight.Passengers.Count];
        Array.Fill(leftSeat, -1);
        while (flight.AllOffTick is null)
        {
            Assert.True(flight.Tick < TickLimit, "The flight never emptied.");
            long tick = flight.Tick;
            flight.Step(1);
            foreach (Passenger passenger in flight.Passengers)
            {
                RecordFirst(leftSeat, passenger.Id, passenger.Node != passenger.Manifest.SeatNode, tick);
            }
        }

        foreach (IGrouping<int, Passenger> row in flight.Passengers.GroupBy(passenger => SeatRow(flight, passenger)))
        {
            int[] nearestFirst =
            [
                .. row.OrderBy(passenger => AisleCost(flight, passenger))
                    .ThenBy(passenger => passenger.Manifest.SeatNode)
                    .Select(passenger => passenger.Id),
            ];
            int[] byLeaving = [.. row.OrderBy(passenger => leftSeat[passenger.Id]).Select(passenger => passenger.Id)];
            Assert.Equal(nearestFirst, byLeaving);
        }
    }

    /// <summary>A passenger busy with a higher-priority action when deboarding starts stands once that action ends.</summary>
    [Fact]
    public void ABusyPassengerStandsOnceFree()
    {
        NavGraph graph = Graph();
        const long busyUntil = DeboardingTick + 50;
        var flight = new FlightWorld(
            Setup(
                ManifestOf(
                    graph,
                    [
                        [SeatIn(graph, 5, 0)],
                    ]
                ),
                Seed
            )
        );
        RunUntil(flight, world => world.AllSeatedTick is not null);
        Passenger passenger = flight.Passengers[0];
        Assert.True(flight.Executor.TryStart(passenger.CharacterId, new BusyAction(busyUntil), flight.Tick));

        RunUntil(flight, world => world.AllOffTick is not null);

        Assert.True(passenger.IsOff);
        Assert.True(flight.AllOffTick > busyUntil, $"The passenger was off at {flight.AllOffTick}, while still busy.");
    }

    /// <summary>A move interrupted on any tick, boarding or deboarding, leaves the character resting and holding only its own node.</summary>
    /// <param name="deboarding">Whether the interrupted move is the deboarding one rather than the boarding one.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnInterruptedMoveLeavesAConsistentState(bool deboarding)
    {
        for (int stop = 0; stop <= 40; stop++)
        {
            FlightWorld flight = CrewMoveInterruptedAt(deboarding, stop);
            AssertResting(flight, 1, $"interrupted after {stop} ticks");
        }
    }

    /// <summary>Every business passenger enters the door before any economy passenger.</summary>
    [Fact]
    public void BusinessBoardsFirst()
    {
        FlightWorld flight = RunBoarding(Setup(FullManifest(), Seed), out long[] entryTicks, out _);

        long lastBusiness = EntryTicksWhere(flight, entryTicks, passenger => passenger.Manifest.SeatClass == SeatClass.Business).Max();
        long firstEconomy = EntryTicksWhere(flight, entryTicks, passenger => passenger.Manifest.SeatClass == SeatClass.Economy).Min();

        Assert.True(lastBusiness < firstEconomy, $"Business entered until tick {lastBusiness}, economy from tick {firstEconomy}.");
    }

    /// <summary>With four-row zones the back zone, rows 6 to 9, enters before any passenger of rows 2 to 5.</summary>
    [Fact]
    public void EconomyBoardsBackToFrontByZones()
    {
        FlightWorld flight = RunBoarding(Setup(FullManifest(), Seed), out long[] entryTicks, out _);

        long lastBackZone = EntryTicksWhere(flight, entryTicks, passenger => SeatRow(flight, passenger) is >= 6 and <= 9).Max();
        long firstFrontZone = EntryTicksWhere(flight, entryTicks, passenger => SeatRow(flight, passenger) is >= 2 and <= 5).Min();

        Assert.True(lastBackZone < firstFrontZone, $"Rows 6-9 entered until tick {lastBackZone}, rows 2-5 from tick {firstFrontZone}.");
    }

    /// <summary>The members of every booking enter the door on consecutive entries.</summary>
    [Fact]
    public void ABookingBoardsTogether()
    {
        PassengerManifest manifest = FullManifest();
        _ = RunBoarding(Setup(manifest, Seed), out long[] entryTicks, out _);
        Assert.Equal(entryTicks.Length, entryTicks.Distinct().Count());
        int[] entryRank = new int[entryTicks.Length];
        int[] byEntry = [.. Enumerable.Range(0, entryTicks.Length).OrderBy(id => entryTicks[id])];
        for (int rank = 0; rank < byEntry.Length; rank++)
        {
            entryRank[byEntry[rank]] = rank;
        }

        Assert.All(
            manifest.Bookings,
            booking =>
            {
                int[] ranks = [.. booking.PassengerIds.Select(id => entryRank[id])];
                Assert.Equal(booking.PassengerIds.Count - 1, ranks.Max() - ranks.Min());
            }
        );
    }

    /// <summary>Two flights from one seed seat everyone on the same ticks.</summary>
    [Fact]
    public void SameSeedSameBoarding()
    {
        FlightWorld first = RunBoarding(Setup(FullManifest(), Seed), out _, out long[] firstSeatTicks);
        FlightWorld second = RunBoarding(Setup(FullManifest(), Seed), out _, out long[] secondSeatTicks);

        Assert.NotNull(first.AllSeatedTick);
        Assert.Equal(first.AllSeatedTick, second.AllSeatedTick);
        Assert.Equal(firstSeatTicks, secondSeatTicks);
    }

    /// <summary>A walker passing a crew member standing in the aisle arrives 4 ticks later than one walking an empty aisle.</summary>
    [Fact]
    public void ASqueezeCostsFourTicks()
    {
        long unobstructed = CrewArrivalTick(_ => { });
        long squeezed = CrewArrivalTick(flight => Assert.True(flight.Movement.TryEnter(1, flight.Graph.AisleSlot(4, 0))));

        Assert.Equal(unobstructed + Rules().SqueezeExtraTicks, squeezed);
    }

    /// <summary>A negative crew count is refused under the setup's field name.</summary>
    [Fact]
    public void ANegativeCrewCountIsRefused()
    {
        FlightSetup setup = Setup(FullManifest(), Seed) with { CrewCount = -1 };

        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() => new FlightWorld(setup));

        Assert.Equal(nameof(FlightSetup.CrewCount), error.ParamName);
    }

    /// <summary>Each bad movement number is refused as it is set, naming its field.</summary>
    /// <param name="bad">The bad case.</param>
    /// <param name="field">The field it names.</param>
    [Theory]
    [InlineData("StowReversed", nameof(MovementRules.StowTicks))]
    [InlineData("StowNegative", nameof(MovementRules.StowTicks))]
    [InlineData("RetrievalReversed", nameof(MovementRules.RetrievalTicks))]
    [InlineData("SqueezeNegative", nameof(MovementRules.SqueezeExtraTicks))]
    [InlineData("CrossNegative", nameof(MovementRules.OccupiedSeatCrossTicks))]
    [InlineData("ZoneRowsZero", nameof(MovementRules.EconomyZoneRows))]
    public void ABadMovementRuleIsRefused(string bad, string field)
    {
        ArgumentException error = Assert.ThrowsAny<ArgumentException>(() => BadRules[bad](Rules()));

        Assert.Equal(field, error.ParamName);
    }

    /// <summary>A layout with no door, or with a booked seat no aisle reaches, is refused by the flight, naming the layout.</summary>
    /// <param name="withoutDoor">Whether the layout lacks a door, rather than having a seat cut off from the aisle.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ABadLayoutIsRefused(bool withoutDoor)
    {
        CabinLayout layout = withoutDoor ? TenRowLayout() with { Fixtures = [] } : CutOffSeatLayout();
        NavGraph graph = NavGraphBuilder.Build(layout, 10);
        FlightSetup setup = Setup(
            ManifestOf(
                graph,
                [
                    [SeatIn(graph, 9, 0)],
                ]
            ),
            Seed
        ) with
        {
            Layout = layout,
        };

        ArgumentException error = Assert.ThrowsAny<ArgumentException>(() => new FlightWorld(setup));

        Assert.Equal(nameof(FlightSetup.Layout), error.ParamName);
    }

    /// <summary>
    /// A two-passenger flight of one booking, in row 5's and row 9's window seats, with the stow draw pinned to the same number
    /// of ticks at both ends.
    /// </summary>
    /// <param name="stowTicks">The stow draw.</param>
    /// <returns>The flight, before the boarding stage starts.</returns>
    private static FlightWorld StowPinnedFlight(int stowTicks)
    {
        NavGraph graph = Graph();
        return new FlightWorld(
            Setup(
                ManifestOf(
                    graph,
                    [
                        [SeatIn(graph, 5, 0), SeatIn(graph, 9, 0)],
                    ]
                ),
                Seed
            ) with
            {
                Movement = Rules() with { StowTicks = new IntRange(stowTicks, stowTicks) },
            }
        );
    }

    /// <summary>The flight's first passenger's state at the end of every tick, tick 0 first, running until every passenger is seated.</summary>
    /// <param name="flight">The flight.</param>
    /// <returns>The tick ends, in order.</returns>
    private static List<MoverState> StowerTickEnds(FlightWorld flight)
    {
        Passenger stower = flight.Passengers[0];
        List<MoverState> states = [];
        while (flight.AllSeatedTick is null)
        {
            Assert.True(flight.Tick < TickLimit, "Boarding never finished.");
            flight.Step(1);
            states.Add(flight.Movement.StateOf(stower.CharacterId));
        }

        return states;
    }

    /// <summary>The ticks at whose end the stower reads as stowing, among its per-tick states.</summary>
    /// <param name="tickEnds">The stower's state at the end of every tick, tick 0 first.</param>
    /// <returns>The ticks, in order.</returns>
    private static List<int> StowingTickEnds(IReadOnlyList<MoverState> tickEnds) =>
        [.. tickEnds.Select((state, tick) => (state, tick)).Where(entry => entry.state == MoverState.Stowing).Select(entry => entry.tick)];

    /// <summary>The ten-row cabin with row 9's seat A in a group of its own, cut off from the aisle by the group B-C beside it.</summary>
    private static CabinLayout CutOffSeatLayout()
    {
        CabinLayout layout = TenRowLayout();
        var cutOff = new CabinRow(
            31,
            [NavGraphBuilderTests.Group(4, 18, "A"), NavGraphBuilderTests.Group(22, 18, "B", "C"), NavGraphBuilderTests.Group(90, 18, "D", "E", "F")]
        );
        return layout with { Rows = [.. layout.Rows.Take(9), cutOff] };
    }

    /// <summary>A row's seat node at a place from the left, 0 for the window seat.</summary>
    private static int SeatIn(NavGraph graph, int row, int fromLeft) =>
        graph.SeatNodes.Where(seat => graph.Nodes[seat].RowIndex == row).ElementAt(fromLeft);

    private static int SeatRow(FlightWorld flight, Passenger passenger) => flight.Graph.Nodes[passenger.Manifest.SeatNode].RowIndex;

    private static int AisleCost(FlightWorld flight, Passenger passenger) =>
        flight.Paths.Cost(passenger.Manifest.SeatNode, BoardingSlot(flight, passenger.Manifest.SeatNode));

    /// <summary>The aisle slot a seat's row boards and leaves through: the first aisle slot on the cheapest walk from the seat to the door.</summary>
    private static int BoardingSlot(FlightWorld flight, int seat)
    {
        int door = flight.Graph.FixtureNode("door-1L");
        int node = seat;
        while (flight.Graph.Nodes[node].Kind != NodeKind.AisleSlot)
        {
            node = flight.Paths.NextHop(node, door);
            if (node < 0 || node == door)
            {
                throw new InvalidOperationException($"Seat node {seat} has no aisle slot on its way to the door.");
            }
        }

        return node;
    }

    private static IEnumerable<long> EntryTicksWhere(FlightWorld flight, long[] entryTicks, Func<Passenger, bool> predicate) =>
        flight.Passengers.Where(predicate).Select(passenger => entryTicks[passenger.Id]);

    /// <summary>A full flight whose feed never leaves pre-boarding, for moving crew by hand.</summary>
    private static FlightWorld NeverBoardingFlight() => new(Setup(FullManifest(), Seed) with { Feed = new StagedFeed(long.MaxValue, long.MaxValue) });

    /// <summary>Runs a flight until every passenger is seated, recording the tick each entered the door and each sat down.</summary>
    private static FlightWorld RunBoarding(FlightSetup setup, out long[] entryTicks, out long[] seatTicks)
    {
        var flight = new FlightWorld(setup);
        entryTicks = new long[flight.Passengers.Count];
        seatTicks = new long[flight.Passengers.Count];
        Array.Fill(entryTicks, -1);
        Array.Fill(seatTicks, -1);
        while (flight.AllSeatedTick is null)
        {
            Assert.True(flight.Tick < TickLimit, "Boarding never finished.");
            long tick = flight.Tick;
            flight.Step(1);
            foreach (Passenger passenger in flight.Passengers)
            {
                RecordFirst(entryTicks, passenger.Id, passenger.IsBoarded, tick);
                RecordFirst(seatTicks, passenger.Id, passenger.Node == passenger.Manifest.SeatNode, tick);
            }
        }

        return flight;
    }

    private static void RecordFirst(long[] ticks, int id, bool happened, long tick)
    {
        if (happened && ticks[id] < 0)
        {
            ticks[id] = tick;
        }
    }

    private static void AssertOnTheGraph(FlightWorld flight)
    {
        int nodeCount = flight.Graph.Nodes.Count;
        foreach (Passenger passenger in flight.Passengers.Where(passenger => passenger.IsBoarded))
        {
            int node = passenger.Node!.Value;
            Assert.InRange(node, 0, nodeCount - 1);
            Assert.True(
                flight.Occupancy.HasArrived(node, passenger.CharacterId),
                $"Passenger {passenger.Id} stands on node {node} without holding it."
            );
        }

        for (int node = 0; node < nodeCount; node++)
        {
            foreach (int holder in flight.Occupancy.Holders(node))
            {
                AssertHolder(flight, node, holder);
            }
        }
    }

    private static void AssertHolder(FlightWorld flight, int node, int holder)
    {
        int crewCount = flight.Passengers[0].CharacterId;
        Assert.InRange(holder, crewCount, crewCount + flight.Passengers.Count - 1);
        Passenger passenger = flight.Passengers[holder - crewCount];
        Assert.True(passenger.IsBoarded, $"Node {node} is held by passenger {passenger.Id}, who is not aboard.");
        bool arrived = flight.Occupancy.HasArrived(node, holder);
        Assert.True(
            arrived == (passenger.Node == node),
            $"Passenger {passenger.Id} holds node {node} (arrived: {arrived}) but stands on {passenger.Node}."
        );
    }

    /// <summary>A character with no movement under way is resting, and holds its own node, arrived, and no other.</summary>
    private static void AssertResting(FlightWorld flight, int character, string when)
    {
        MoverState state = flight.Movement.StateOf(character);
        Assert.True(state is MoverState.Standing or MoverState.Seated or MoverState.Off, $"{when}: the character is {state}.");
        int? node = flight.Movement.NodeOf(character);
        for (int other = 0; other < flight.Graph.Nodes.Count; other++)
        {
            bool expected = other == node;
            Assert.True(flight.Occupancy.Holds(other, character) == expected, $"{when}: holding node {other} is {!expected}.");
            Assert.True(!expected || flight.Occupancy.HasArrived(other, character), $"{when}: the character has not arrived at its node {other}.");
        }
    }

    /// <summary>
    /// Enters crew member 1 at the door and runs its boarding move to row 3's window seat (and, when deboarding, back out the
    /// door), interrupting it with a busy action after <paramref name="stop"/> ticks of the move being interrupted.
    /// </summary>
    private static FlightWorld CrewMoveInterruptedAt(bool deboarding, int stop)
    {
        const int crew = 1;
        FlightWorld flight = NeverBoardingFlight();
        Movement mover = flight.Movement;
        int door = flight.Graph.FixtureNode("door-1L");
        int slot = flight.Graph.AisleSlot(3, 0);
        int seat = SeatIn(flight.Graph, 3, 0);
        SimRandom stream = flight.Rng.Stream("test");
        var threeTicks = new IntRange(3, 3);
        Assert.True(mover.TryEnter(crew, door));
        var board = new ActionChain([
            () => mover.Walk(crew, slot, queueWhenBlocked: false),
            () => mover.Hold(crew, MoverState.Stowing, threeTicks, stream),
            () => mover.Cross(crew, seat, MoverState.Seated),
        ]);
        Assert.True(flight.Executor.TryStart(crew, board, flight.Tick));
        if (deboarding)
        {
            RunUntil(flight, world => world.Movement.StateOf(crew) == MoverState.Seated);
            var leave = new ActionChain([
                () => mover.Cross(crew, slot, MoverState.Standing),
                () => mover.Hold(crew, MoverState.Retrieving, threeTicks, stream),
                () => mover.Walk(crew, door, queueWhenBlocked: true),
                () => mover.Exit(crew),
            ]);
            Assert.True(flight.Executor.TryStart(crew, leave, flight.Tick));
        }

        flight.Step(stop);
        Assert.True(flight.Executor.TryStart(crew, new BusyAction(long.MaxValue), flight.Tick));
        return flight;
    }

    /// <summary>
    /// Walks crew member 0 from row 2's aisle slot to row 6's on a flight that never boards, after <paramref name="obstacle"/>
    /// has placed whatever stands in the way, and returns the tick the walker arrives.
    /// </summary>
    private static long CrewArrivalTick(Action<FlightWorld> obstacle)
    {
        FlightWorld flight = NeverBoardingFlight();
        obstacle(flight);
        Assert.True(flight.Movement.TryEnter(0, flight.Graph.AisleSlot(2, 0)));
        int target = flight.Graph.AisleSlot(6, 0);
        Assert.True(flight.Executor.TryStart(0, flight.Movement.Walk(0, target, queueWhenBlocked: false), flight.Tick));
        RunUntil(flight, world => world.Movement.NodeOf(0) == target);
        return flight.Tick;
    }

    /// <summary>Walks crew 0 aft from row 2's slot to row 7's, crew 1 forward from row 7's to row 2's, or both; returns the arrival ticks.</summary>
    private static (long Aft, long Forward) OppositeWalkArrivals(bool walkAft, bool walkForward)
    {
        FlightWorld flight = NeverBoardingFlight();
        int rowTwo = flight.Graph.AisleSlot(2, 0);
        int rowSeven = flight.Graph.AisleSlot(7, 0);
        StartWalk(flight, 0, rowTwo, rowSeven, walkAft);
        StartWalk(flight, 1, rowSeven, rowTwo, walkForward);
        long[] arrivals = [walkAft ? -1 : 0, walkForward ? -1 : 0];
        int[] targets = [rowSeven, rowTwo];
        RunUntil(flight, world => RecordArrivals(world, targets, arrivals));
        return (arrivals[0], arrivals[1]);
    }

    /// <summary>Records the tick each walker, crew 0 and 1, is first at its target; true once every walker has arrived.</summary>
    private static bool RecordArrivals(FlightWorld flight, int[] targets, long[] arrivals)
    {
        for (int crew = 0; crew < arrivals.Length; crew++)
        {
            if (arrivals[crew] < 0 && flight.Movement.NodeOf(crew) == targets[crew])
            {
                arrivals[crew] = flight.Tick;
            }
        }

        return arrivals.All(tick => tick >= 0);
    }

    private static void StartWalk(FlightWorld flight, int crew, int from, int to, bool walk)
    {
        if (walk)
        {
            Assert.True(flight.Movement.TryEnter(crew, from));
            Assert.True(flight.Executor.TryStart(crew, flight.Movement.Walk(crew, to, queueWhenBlocked: false), flight.Tick));
        }
    }

    /// <summary>The ticks from a passenger's last tick stowing to the tick they are in their seat: their cross to the seat.</summary>
    private static long WindowCrossTicks(PassengerManifest manifest, int windowPassenger)
    {
        var flight = new FlightWorld(Setup(manifest, Seed));
        Passenger passenger = flight.Passengers[windowPassenger];
        long lastStowing = -1;
        while (passenger.Node != passenger.Manifest.SeatNode)
        {
            Assert.True(flight.Tick < TickLimit, "The window passenger never sat.");
            long tick = flight.Tick;
            flight.Step(1);
            lastStowing = flight.Movement.StateOf(passenger.CharacterId) == MoverState.Stowing ? tick : lastStowing;
        }

        return flight.Tick - lastStowing;
    }

    /// <summary>A higher-priority action than any movement, running until a tick.</summary>
    private sealed class BusyAction(long untilTick) : CharacterAction(BusyPriority)
    {
        public override ActionStatus Tick(long tick) => tick >= untilTick ? ActionStatus.Done : ActionStatus.Running;
    }
}
