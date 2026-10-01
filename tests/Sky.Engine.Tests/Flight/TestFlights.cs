using Sky.Engine.Cabin;
using Sky.Engine.Flight;
using Sky.Engine.Manifest;
using Sky.Engine.Needs;
using Sky.Engine.Passengers;
using Sky.Engine.Ports;
using Sky.Engine.Tests.Cabin;
using Sky.Engine.Tests.Fakes;
using Sky.Engine.Tests.Passengers;

namespace Sky.Engine.Tests.Flight;

/// <summary>
/// Flights with real boarding for the flight tests: the ten-row test cabin, a manifest filling it, a feed that boards at
/// <see cref="BoardingTick"/> and deboards at <see cref="DeboardingTick"/>, and the numbers every such flight runs on.
/// </summary>
internal static class TestFlights
{
    /// <summary>The rows of the test cabin seated in business, at the front.</summary>
    internal const int BusinessRows = 2;

    /// <summary>The tick the feed moves to boarding.</summary>
    internal const long BoardingTick = 10;

    /// <summary>The tick the feed moves to deboarding, well after every passenger is seated.</summary>
    internal const long DeboardingTick = 20_000;

    /// <summary>The most ticks a test runs before it gives up on the condition it waits for.</summary>
    internal const long TickLimit = 60_000;

    /// <summary>The <c>idle</c> activity, every passenger's initial one.</summary>
    internal static readonly ActivityId Idle = new(0);

    /// <summary>The <c>lav_visit</c> activity.</summary>
    internal static readonly ActivityId LavVisit = new(1);

    /// <summary>The <c>screen</c> activity.</summary>
    internal static readonly ActivityId Screen = new(2);

    private static readonly NodeCapacities Capacities = new()
    {
        AisleSlot = 1,
        Seat = 1,
        Door = 2,
        Lav = 1,
        LavQueue = 3,
        Galley = 2,
    };

    /// <summary>Booking sizes, cycled along each row.</summary>
    private static readonly int[] BookingSizes = [2, 1, 3];

    /// <summary>The movement numbers the tests use: stowing 8 to 24 ticks, retrieval 4 to 12, a 4-tick squeeze, four-row zones.</summary>
    /// <returns>The rules.</returns>
    internal static MovementRules Rules() =>
        new()
        {
            StowTicks = new IntRange(8, 24),
            RetrievalTicks = new IntRange(4, 12),
            SqueezeExtraTicks = 4,
            OccupiedSeatCrossTicks = 4,
            EconomyZoneRows = 4,
        };

    /// <summary>
    /// The decision numbers the tests use, balance.md sections 3.2 and 3.9: a decision every 480 ticks, a keep-current bias of 4,
    /// candidates <see cref="Idle"/>, <see cref="LavVisit"/> and <see cref="Screen"/>, starting idle.
    /// </summary>
    /// <returns>The rules.</returns>
    internal static DecisionRules Decisions() =>
        new()
        {
            CadenceTicks = 480,
            KeepCurrentBias = 4,
            Candidates = [Idle, LavVisit, Screen],
            Initial = Idle,
        };

    /// <summary>
    /// Builds the ten-row test cabin: rows 0 and 1 are 2-2 business rows, rows 2 to 9 are 3-3 economy rows, one aisle at 74
    /// inches, a forward door at row 0 and a galley behind row 9.
    /// </summary>
    /// <returns>The layout.</returns>
    internal static CabinLayout TenRowLayout()
    {
        var business = new CabinRow(36, [NavGraphBuilderTests.Group(6, 26, "A", "C"), NavGraphBuilderTests.Group(90, 26, "D", "F")]);
        var economy = new CabinRow(31, [NavGraphBuilderTests.Group(4, 18, "A", "B", "C"), NavGraphBuilderTests.Group(90, 18, "D", "E", "F")]);
        return new CabinLayout(
            "ten-row",
            148,
            [.. Enumerable.Repeat(business, BusinessRows), .. Enumerable.Repeat(economy, 8)],
            [new Aisle(74, 20)],
            [new CabinFixture("door-1L", FixtureKind.Door, 0, 0, 30), new CabinFixture("galley-aft", FixtureKind.Galley, 9, 0, 30)]
        );
    }

    /// <summary>The ten-row cabin's nav graph.</summary>
    /// <returns>The graph.</returns>
    internal static NavGraph Graph() => NavGraphBuilder.Build(TenRowLayout(), 10);

    /// <summary>A manifest filling every seat, each row split into bookings of 2, 1 and 3 in turn, no booking spanning two rows.</summary>
    /// <returns>The manifest.</returns>
    internal static PassengerManifest FullManifest()
    {
        NavGraph graph = Graph();
        List<int[]> bookings = [];
        foreach (IGrouping<int, int> row in graph.SeatNodes.GroupBy(seat => graph.Nodes[seat].RowIndex))
        {
            int[] seats = [.. row];
            int taken = 0;
            for (int turn = 0; taken < seats.Length; turn++)
            {
                int size = Math.Min(BookingSizes[turn % BookingSizes.Length], seats.Length - taken);
                bookings.Add(seats[taken..(taken + size)]);
                taken += size;
            }
        }

        return ManifestOf(graph, bookings);
    }

    /// <summary>A manifest of the given bookings, each a list of seat nodes; passengers in rows 0 and 1 sit in business.</summary>
    /// <param name="graph">The ten-row cabin's nav graph.</param>
    /// <param name="bookingSeats">Each booking's seat nodes, in passenger order.</param>
    /// <returns>The manifest.</returns>
    internal static PassengerManifest ManifestOf(NavGraph graph, IReadOnlyList<int[]> bookingSeats)
    {
        List<ManifestPassenger> passengers = [];
        List<Booking> bookings = [];
        foreach (int[] seats in bookingSeats)
        {
            SeatClass seatClass = graph.Nodes[seats[0]].RowIndex < BusinessRows ? SeatClass.Business : SeatClass.Economy;
            int[] ids = [.. Enumerable.Range(passengers.Count, seats.Length)];
            passengers.AddRange(seats.Select((seat, index) => Passenger(ids[index], bookings.Count, seatClass, seat)));
            bookings.Add(new Booking(bookings.Count, TripPurpose.Leisure, seatClass, 390, ids));
        }

        return new PassengerManifest(passengers, bookings);
    }

    /// <summary>
    /// A flight in the ten-row cabin with two crew, boarding at <see cref="BoardingTick"/> and deboarding at <see cref="DeboardingTick"/>,
    /// scored by a <see cref="FakeBehaviorScripts"/> that scores every activity 0.
    /// </summary>
    /// <param name="manifest">The passengers.</param>
    /// <param name="seed">The run's root seed.</param>
    /// <returns>The setup.</returns>
    internal static FlightSetup Setup(PassengerManifest manifest, ulong seed) =>
        new()
        {
            Layout = TenRowLayout(),
            InchesPerTick = 10,
            Capacities = Capacities,
            Manifest = manifest,
            CrewCount = 2,
            Seed = seed,
            Feed = new StagedFeed(BoardingTick, DeboardingTick),
            Scripts = new FakeBehaviorScripts(),
            NeedRates = new NeedRates(
                new NeedRateSettings
                {
                    RefreshmentPerHour = 25,
                    BladderPerHour = 20,
                    RestRisePerHour = 5,
                    RestFallPerHour = 10,
                    BoredomPerHour = 15,
                    UneaseHalfLifeMinutes = 15,
                }
            ),
            AboardUneasePushPerHour = 30,
            StartingNeeds = StartingNeedsTests.Rules(),
            Conditions = StartingNeedsTests.Conditions(0, true),
            Traits = StartingNeedsTests.Traits(),
            Movement = Rules(),
            Decisions = Decisions(),
        };

    /// <summary>Steps a flight one tick at a time until a condition holds, failing past <see cref="TickLimit"/>.</summary>
    /// <param name="flight">The flight.</param>
    /// <param name="done">The condition.</param>
    internal static void RunUntil(FlightWorld flight, Func<FlightWorld, bool> done)
    {
        while (!done(flight))
        {
            Assert.True(flight.Tick < TickLimit, $"The condition was not met by tick {TickLimit}.");
            flight.Step(1);
        }
    }

    private static ManifestPassenger Passenger(int id, int bookingId, SeatClass seatClass, int seat) =>
        new()
        {
            Id = id,
            BookingId = bookingId,
            TripPurpose = TripPurpose.Leisure,
            AgeBand = AgeBand.Adult,
            SeatClass = seatClass,
            Profession = null,
            Traits = Array.Empty<TraitId>(),
            SeatNode = seat,
            WakeMinute = 390,
        };

    /// <summary>A feed in pre-boarding, boarding from <paramref name="boardingTick"/>, deboarding from <paramref name="deboardingTick"/>.</summary>
    /// <param name="boardingTick">The tick boarding starts.</param>
    /// <param name="deboardingTick">The tick deboarding starts.</param>
    internal sealed class StagedFeed(long boardingTick, long deboardingTick) : ISimFeed
    {
        /// <inheritdoc/>
        public FeedObservation Observe(long tick)
        {
            FlightStage stage =
                tick >= deboardingTick ? FlightStage.Deboarding
                : tick >= boardingTick ? FlightStage.Boarding
                : FlightStage.PreBoarding;
            return new FeedObservation(stage, false, Turbulence.None);
        }
    }
}
